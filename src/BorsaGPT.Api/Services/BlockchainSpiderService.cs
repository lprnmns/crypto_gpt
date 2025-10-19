using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using BorsaGPT.Api.Data;
using BorsaGPT.Api.Models;

namespace BorsaGPT.Api.Services;

/// <summary>
/// Continuously scans Ethereum blocks, looking for large ETH and ERC-20 transfers.
/// Detected wallets are stored in candidate_wallets for further analysis.
/// </summary>
public class BlockchainSpiderService : BackgroundService
{
    private const string TransferEventSignature = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

    private readonly ILogger<BlockchainSpiderService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TokenPriceService _tokenPriceService;
    private readonly HttpClient _httpClient;
    private readonly string _stateFilePath;
    private readonly object _stateSync = new();
    private readonly decimal _configuredMinTransferUsd;
    private readonly long? _configuredStartBlock;

    private Web3? _web3;
    private string _rpcUrl = string.Empty;
    private long _lastProcessedBlock;
    private List<TokenConfig> _tokens = new();

    private readonly HashSet<string> _ignoredAddresses = new(StringComparer.OrdinalIgnoreCase)
    {
        "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2", // WETH
        "0xdac17f958d2ee523a2206206994597c13d831ec7", // USDT
        "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48", // USDC
        "0x6b175474e89094c44da98b954eedeac495271d0f", // DAI
        "0x2260fac5e5542a773aa44fbcfedf7c193bc2c599", // WBTC
        "0x514910771af9ca656af840dff83e8264ecf986ca", // LINK
        "0x1f9840a85d5af5bf1d1762f925bdaddc4201f984", // UNI
        "0x95ad61b0a150d79219dcf64e1e6cc01f0b64c4ce", // SHIB
        "0x0000000000000000000000000000000000000000"
    };

    public BlockchainSpiderService(
        ILogger<BlockchainSpiderService> logger,
        IConfiguration configuration,
        IServiceScopeFactory serviceScopeFactory,
        IHttpClientFactory httpClientFactory,
        TokenPriceService tokenPriceService)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceScopeFactory = serviceScopeFactory;
        _tokenPriceService = tokenPriceService;
        _httpClient = httpClientFactory.CreateClient();

        _stateFilePath = configuration["Spider:StateFilePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "spider_state.json");

        _configuredMinTransferUsd = configuration.GetValue<decimal>("Spider:MinimumTransferUsd", 30_000m);
        var startBlock = configuration.GetValue<long>("Spider:StartBlock", 0);
        _configuredStartBlock = startBlock > 0 ? startBlock : null;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[SPIDER] Starting background spider service...");

        _rpcUrl = _configuration["Alchemy:RpcUrl"] ?? throw new InvalidOperationException("Alchemy:RpcUrl configuration is missing");
        _web3 = new Web3(_rpcUrl);

        _tokens = _configuration.GetSection("Tokens").Get<List<TokenConfig>>() ?? new List<TokenConfig>();
        _logger.LogInformation("[SPIDER] Loaded {Count} static token configs: {Tokens}", _tokens.Count, string.Join(", ", _tokens.Select(t => t.Symbol)));

        await LoadSpiderStateAsync();

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_web3 == null)
        {
            _logger.LogError("[SPIDER] Web3 client not initialised. Aborting background loop.");
            return;
        }

        if (_lastProcessedBlock <= 0)
        {
            if (_configuredStartBlock.HasValue)
            {
                _lastProcessedBlock = _configuredStartBlock.Value - 1;
                _logger.LogInformation("[SPIDER] Starting from configured block {Block}", _configuredStartBlock);
            }
            else
            {
                var latestBlock = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                _lastProcessedBlock = (long)latestBlock.Value - 10;
                _logger.LogInformation("[SPIDER] Starting from recent block {Block}", _lastProcessedBlock);
            }
        }
        else
        {
            _logger.LogInformation("[SPIDER] Resuming from persisted block {Block}", _lastProcessedBlock);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNewBlocksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SPIDER] Error while processing blocks");
            }

            var intervalSeconds = _configuration.GetValue("Spider:PollingIntervalSeconds", 15);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[SPIDER] Stopping background spider service...");
        await PersistSpiderStateAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task ProcessNewBlocksAsync(CancellationToken stoppingToken)
    {
        if (_web3 == null)
        {
            return;
        }

        var latestBlockHex = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        var latestBlock = (long)latestBlockHex.Value;

        if (latestBlock <= _lastProcessedBlock)
        {
            _logger.LogDebug("[SPIDER] No new blocks. Latest={Latest} LastProcessed={Last}", latestBlock, _lastProcessedBlock);
            return;
        }

        _logger.LogInformation("[SPIDER] Scanning blocks {From} -> {To} ({Count} blocks)",
            _lastProcessedBlock + 1,
            latestBlock,
            latestBlock - _lastProcessedBlock);

        for (long blockNumber = _lastProcessedBlock + 1; blockNumber <= latestBlock; blockNumber++)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await ProcessBlockAsync(blockNumber);
        }

        _lastProcessedBlock = latestBlock;
        await PersistSpiderStateAsync();
    }

    private async Task ProcessBlockAsync(long blockNumber)
    {
        if (_web3 == null)
        {
            return;
        }

        try
        {
            var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger(blockNumber));
            if (block?.Transactions == null || block.Transactions.Length == 0)
            {
                return;
            }

            _logger.LogDebug("[SPIDER] Block #{BlockNumber} contains {TxCount} transactions", blockNumber, block.Transactions.Length);

            foreach (var tx in block.Transactions)
            {
                await ProcessEthTransactionAsync(tx, blockNumber);
            }

            await ProcessTokenTransfersAsync(blockNumber);
            await ProcessAssetTransfersAsync(blockNumber);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SPIDER] Failed to process block #{Block}", blockNumber);
        }
    }

    private async Task ProcessEthTransactionAsync(Transaction tx, long blockNumber)
    {
        var valueInEth = Web3.Convert.FromWei(tx.Value);
        var ethPriceUsd = _configuration.GetValue("Spider:EthPriceUsd", 2_500m);
        var valueInUsd = valueInEth * ethPriceUsd;

        if (valueInUsd < _configuredMinTransferUsd)
        {
            return;
        }

        _logger.LogInformation("[SPIDER] Large ETH transfer: {Value} ETH (${Usd}) | {From} -> {To}", valueInEth, valueInUsd, tx.From, tx.To);

        await SaveCandidateWalletAsync(tx.From, valueInEth, null, blockNumber, 18);

        if (!string.IsNullOrEmpty(tx.To))
        {
            await SaveCandidateWalletAsync(tx.To, valueInEth, null, blockNumber, 18);
        }
    }

    private async Task ProcessTokenTransfersAsync(long blockNumber)
    {
        if (_web3 == null)
        {
            return;
        }

        foreach (var token in _tokens)
        {
            try
            {
                var eventHandler = _web3.Eth.GetEvent<TransferEventDTO>(token.Address);
                var filterInput = eventHandler.CreateFilterInput(
                    fromBlock: new BlockParameter((ulong)blockNumber),
                    toBlock: new BlockParameter((ulong)blockNumber));

                var events = await eventHandler.GetAllChangesAsync(filterInput);

                foreach (var evt in events)
                {
                    await ProcessTokenTransferEventAsync(evt, token, blockNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SPIDER] Failed to fetch events for token {Token} at block {Block}", token.Symbol, blockNumber);
            }
        }
    }

    private async Task ProcessTokenTransferEventAsync(EventLog<TransferEventDTO> evt, TokenConfig token, long blockNumber)
    {
        try
        {
            var rawValue = evt.Event.Value;
            var tokenAmount = Web3.Convert.FromWei(rawValue, token.Decimals);

            var tokenPrice = await _tokenPriceService.GetPriceAsync(token.Symbol, token.IsStablecoin);
            if (tokenPrice <= 0)
            {
                return;
            }

            var valueInUsd = tokenAmount * tokenPrice;
            if (valueInUsd < _configuredMinTransferUsd)
            {
                return;
            }

            _logger.LogInformation("[SPIDER] Large token transfer: {Amount} {Symbol} (${Usd}) | {From} -> {To}",
                tokenAmount, token.Symbol, valueInUsd, evt.Event.From, evt.Event.To);

            await SaveCandidateWalletAsync(evt.Event.From, tokenAmount, token.Address, blockNumber, token.Decimals);
            await SaveCandidateWalletAsync(evt.Event.To, tokenAmount, token.Address, blockNumber, token.Decimals);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SPIDER] Failed to process token transfer for {Symbol}", token.Symbol);
        }
    }

    private async Task ProcessAssetTransfersAsync(long blockNumber)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "alchemy_getAssetTransfers",
                @params = new object[]
                {
                    new
                    {
                        fromBlock = $"0x{blockNumber:X}",
                        toBlock = $"0x{blockNumber:X}",
                        category = new[] { "erc20" },
                        maxCount = "0x3e8",
                        withMetadata = false,
                        excludeZeroValue = true
                    }
                }
            };

            using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_rpcUrl, content);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("[SPIDER] Alchemy asset transfer rate limit at block {Block}", blockNumber);
                return;
            }

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);

            if (!doc.RootElement.TryGetProperty("result", out var result) ||
                !result.TryGetProperty("transfers", out var transfers))
            {
                return;
            }

            foreach (var transfer in transfers.EnumerateArray())
            {
                var rawContract = transfer.GetProperty("rawContract");
                var tokenAddress = rawContract.TryGetProperty("address", out var addressProp) ? addressProp.GetString() : null;

                if (string.IsNullOrEmpty(tokenAddress) || _ignoredAddresses.Contains(tokenAddress))
                {
                    continue;
                }

                var from = transfer.TryGetProperty("from", out var fromProp) ? fromProp.GetString() : null;
                var to = transfer.TryGetProperty("to", out var toProp) ? toProp.GetString() : null;

                if (string.IsNullOrEmpty(from) && string.IsNullOrEmpty(to))
                {
                    continue;
                }

                var decimals = TryParseInt(rawContract, "decimals") ?? 18;
                var amount = ParseTransferAmount(transfer, rawContract, decimals);
                if (amount <= 0)
                {
                    continue;
                }

                var symbol = transfer.TryGetProperty("asset", out var assetProp) ? assetProp.GetString() : null;
                var isStable = symbol != null && symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase);
                var price = symbol != null ? await _tokenPriceService.GetPriceAsync(symbol.ToUpperInvariant(), isStable) : 0m;

                if (price <= 0)
                {
                    continue;
                }

                var valueUsd = amount * price;
                if (valueUsd < _configuredMinTransferUsd)
                {
                    continue;
                }

                _logger.LogInformation("[SPIDER] Dynamic token transfer: {Amount} {Symbol} (${Usd}) | {From} -> {To}",
                    amount,
                    symbol ?? tokenAddress,
                    valueUsd,
                    from,
                    to);

                if (!string.IsNullOrEmpty(from))
                {
                    await SaveCandidateWalletAsync(from, amount, tokenAddress, blockNumber, decimals);
                }

                if (!string.IsNullOrEmpty(to))
                {
                    await SaveCandidateWalletAsync(to, amount, tokenAddress, blockNumber, decimals);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SPIDER] Failed to process asset transfers for block {Block}", blockNumber);
        }
    }

    private async Task SaveCandidateWalletAsync(string walletAddress, decimal transferAmount, string? tokenAddress, long blockNumber, int? tokenDecimals)
    {
        if (string.IsNullOrEmpty(walletAddress) || _ignoredAddresses.Contains(walletAddress))
        {
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BorsaGptDbContext>();

        try
        {
            var existingWallet = await db.CandidateWallets.FirstOrDefaultAsync(c => c.WalletAddress == walletAddress);
            if (existingWallet != null)
            {
                return;
            }

            var candidate = new CandidateWallet
            {
                WalletAddress = walletAddress,
                DetectedAt = DateTime.UtcNow,
                FirstTransferAmountEth = transferAmount,
                FirstTransferToken = tokenAddress,
                FirstTransferTokenDecimals = tokenDecimals,
                BlockNumber = blockNumber,
                Analyzed = false
            };

            db.CandidateWallets.Add(candidate);
            await db.SaveChangesAsync();

            _logger.LogInformation("[SPIDER] Candidate wallet persisted: {Address} (Block #{Block})", walletAddress, blockNumber);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogDebug("[SPIDER] Duplicate candidate skipped: {Address}", walletAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SPIDER] Failed to persist candidate wallet {Address}", walletAddress);
        }
    }

    private async Task LoadSpiderStateAsync()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                _logger.LogInformation("[SPIDER] No existing spider_state file at {Path}.", _stateFilePath);
                return;
            }

            var json = await File.ReadAllTextAsync(_stateFilePath);
            var state = JsonSerializer.Deserialize<SpiderState>(json);

            if (state != null && state.LastProcessedBlock > 0)
            {
                _lastProcessedBlock = state.LastProcessedBlock;
                _logger.LogInformation("[SPIDER] Restored last processed block {Block} from state file", _lastProcessedBlock);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SPIDER] Failed to read spider state file {Path}", _stateFilePath);
        }
    }

    private async Task PersistSpiderStateAsync()
    {
        try
        {
            var state = new SpiderState
            {
                LastProcessedBlock = _lastProcessedBlock,
                UpdatedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });

            lock (_stateSync)
            {
                var directory = Path.GetDirectoryName(_stateFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            await File.WriteAllTextAsync(_stateFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SPIDER] Failed to persist spider state to {Path}", _stateFilePath);
        }
    }

    private static decimal ParseTransferAmount(JsonElement transfer, JsonElement rawContract, int decimals)
    {
        if (transfer.TryGetProperty("value", out var valueProp) &&
            decimal.TryParse(valueProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return parsedValue;
        }

        if (rawContract.TryGetProperty("value", out var rawValueProp))
        {
            var rawValue = rawValueProp.GetString();
            if (!string.IsNullOrEmpty(rawValue))
            {
                try
                {
                    var hex = rawValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? rawValue[2..] : rawValue;
                    var bigInt = BigInteger.Parse(hex, NumberStyles.HexNumber);
                    return Web3.Convert.FromWei(bigInt, decimals);
                }
                catch
                {
                    return 0m;
                }
            }
        }

        return 0m;
    }

    private static int? TryParseInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetInt32(out var value) ? value : null,
            JsonValueKind.String => int.TryParse(property.GetString(), out var parsed) ? parsed : null,
            _ => null
        };
    }

    private sealed record SpiderState
    {
        public long LastProcessedBlock { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}

[Event("Transfer")]
public class TransferEventDTO : IEventDTO
{
    [Parameter("address", "from", 1, true)]
    public string From { get; set; } = string.Empty;

    [Parameter("address", "to", 2, true)]
    public string To { get; set; } = string.Empty;

    [Parameter("uint256", "value", 3, false)]
    public BigInteger Value { get; set; }
}


