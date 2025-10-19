using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nethereum.Web3;
using BorsaGPT.Api.Data;
using BorsaGPT.Api.Models;
using BorsaGPT.Api.Exceptions;

namespace BorsaGPT.Api.Services;

/// <summary>
/// Executes portfolio analytics for candidate wallets between two timestamps.
/// </summary>
public class WalletAnalyzerService
{
    private readonly ILogger<WalletAnalyzerService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly EtherscanService _etherscan;
    private readonly AlchemyHistoricalService _alchemy;
    private readonly PriceHistoryService _priceHistory;
    private readonly ProgressTrackerService _progressTracker;
    private readonly IConfiguration _configuration;

    private readonly DateTime _t0;
    private readonly DateTime _t1;
    private readonly string _analysisWindowKey;
    private readonly long? _t0BlockOverride;
    private readonly long? _t1BlockOverride;

    private readonly HashSet<string> _stablecoins = new(StringComparer.OrdinalIgnoreCase)
    {
        "0xdac17f958d2ee523a2206206994597c13d831ec7", // USDT
        "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48", // USDC
        "0x6b175474e89094c44da98b954eedeac495271d0f", // DAI
        "0x853d955acef822db058eb8505911ed77f175b99e", // FRAX
        "0x5f98805a4e8be255a32880fdec7f6728c6568ba0", // LUSD
        "0x0000000000085d4780b73119b644ae5ecd22b376"  // TUSD
    };

    public WalletAnalyzerService(
        ILogger<WalletAnalyzerService> logger,
        IServiceScopeFactory serviceScopeFactory,
        EtherscanService etherscan,
        AlchemyHistoricalService alchemy,
        PriceHistoryService priceHistory,
        ProgressTrackerService progressTracker,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _etherscan = etherscan;
        _alchemy = alchemy;
        _priceHistory = priceHistory;
        _progressTracker = progressTracker;
        _configuration = configuration;

        var windowSection = configuration.GetSection("AnalysisWindow");
        var windowConfig = new AnalysisWindowConfig
        {
            StartUtc = windowSection.GetValue<DateTime?>("StartUtc"),
            EndUtc = windowSection.GetValue<DateTime?>("EndUtc"),
            ReferenceDateUtc = windowSection.GetValue<DateTime?>("ReferenceDateUtc"),
            T0OffsetHours = windowSection.GetValue<double?>("T0OffsetHours"),
            T1OffsetHours = windowSection.GetValue<double?>("T1OffsetHours"),
            T0Block = windowSection.GetValue<long?>("T0Block"),
            T1Block = windowSection.GetValue<long?>("T1Block")
        };

        try
        {
            (_t0, _t1) = windowConfig.ResolveWindow(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Unable to resolve analysis window from configuration.", ex);
        }

        _analysisWindowKey = windowConfig.BuildWindowKey(_t0, _t1);
        _logger.LogInformation("[ANALYZER] Using window {T0} -> {T1} (key {Key})", _t0, _t1, _analysisWindowKey);
        (_t0BlockOverride, _t1BlockOverride) = windowConfig.GetBlockOverrides();
        if (_t0BlockOverride.HasValue && _t1BlockOverride.HasValue)
        {
            _logger.LogInformation("[ANALYZER] Using configured block overrides t0={T0Block} t1={T1Block}", _t0BlockOverride, _t1BlockOverride);
        }
    }

    public async Task AnalyzeAllWalletsAsync()
    {
        _logger.LogInformation("[ANALYZER] Starting portfolio analysis for window {T0}->{T1}", _t0, _t1);

        long t0Block;
        long t1Block;

        if (_t0BlockOverride.HasValue && _t1BlockOverride.HasValue)
        {
            t0Block = _t0BlockOverride.Value;
            t1Block = _t1BlockOverride.Value;
            _logger.LogInformation("[ANALYZER] Using block overrides t0={T0Block}, t1={T1Block}", t0Block, t1Block);
        }
        else
        {
            try
            {
                (t0Block, t1Block) = await _etherscan.GetBlockRangeAsync(_t0, _t1);
                _logger.LogInformation("[ANALYZER] Resolved block range t0={T0Block}, t1={T1Block}", t0Block, t1Block);
            }
            catch (RateLimitException ex)
            {
                _logger.LogError(ex, "[ANALYZER] Etherscan rate limit while resolving block range");
                throw;
            }
        }

        var progress = await _progressTracker.LoadProgressAsync(_analysisWindowKey);

        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BorsaGptDbContext>();
        var allWallets = await db.CandidateWallets.OrderBy(w => w.Id).ToListAsync();

        if (progress == null)
        {
            progress = _progressTracker.CreateNew(allWallets.Count, _analysisWindowKey);
            await _progressTracker.SaveProgressAsync(progress);
        }
        else
        {
            progress.AnalysisWindowKey = _analysisWindowKey;
        }

        var walletsToProcess = allWallets
            .Where(w => w.Id > progress.LastProcessedWalletId && !w.Analyzed)
            .Take(100)
            .ToList();

        _logger.LogInformation("[ANALYZER] Wallet queue = {Pending} (of {Total})", walletsToProcess.Count, allWallets.Count);

        foreach (var wallet in walletsToProcess)
        {
            var attempt = 0;
            const int maxAttempts = 3;
            var processed = false;

            while (!processed && attempt < maxAttempts)
            {
                try
                {
                    attempt++;
                    _logger.LogInformation("[ANALYZER] Processing wallet {Address} (ID={Id}, attempt={Attempt})", wallet.WalletAddress, wallet.Id, attempt);

                    var analysis = await AnalyzeWalletAsync(wallet, t0Block, t1Block);

                    using var saveScope = _serviceScopeFactory.CreateScope();
                    var saveDb = saveScope.ServiceProvider.GetRequiredService<BorsaGptDbContext>();

                    saveDb.CandidateAnalysis.Add(analysis);

                    var candidate = await saveDb.CandidateWallets.FirstOrDefaultAsync(c => c.Id == wallet.Id);
                    if (candidate != null)
                    {
                        candidate.Analyzed = true;
                        saveDb.CandidateWallets.Update(candidate);
                    }

                    await saveDb.SaveChangesAsync();

                    progress.LastProcessedWalletId = wallet.Id;
                    progress.LastProcessedAddress = wallet.WalletAddress;
                    progress.ProcessedCount++;
                    progress.ErrorMessage = null;

                    await _progressTracker.SaveProgressAsync(progress);

                    if (progress.ProcessedCount % 10 == 0)
                    {
                        _progressTracker.LogProgress(progress);
                    }

                    if (progress.ProcessedCount % 50 == 0)
                    {
                        await ExportResultsToCsvAsync();
                    }

                    processed = true;
                }
                catch (RateLimitException ex) when (attempt < maxAttempts)
                {
                    _logger.LogWarning("[ANALYZER] Rate limit ({Provider}) encountered. Retry {Attempt}/{MaxAttempts} in 60s.", ex.Provider, attempt, maxAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(60));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ANALYZER] Wallet processing failed for {Address}", wallet.WalletAddress);
                    progress.ErrorMessage = $"Error at wallet {wallet.Id}: {ex.Message}";
                    await _progressTracker.SaveProgressAsync(progress);
                    break;
                }
            }
        }

        await _progressTracker.MarkCompletedAsync(progress);
        _logger.LogInformation("[ANALYZER] Analysis complete. Processed {Count} wallets.", progress.ProcessedCount);

        await ExportResultsToCsvAsync();
    }

    private async Task<CandidateAnalysis> AnalyzeWalletAsync(CandidateWallet wallet, long t0Block, long t1Block)
    {
        var analysis = new CandidateAnalysis
        {
            CandidateWalletId = wallet.Id,
            WalletAddress = wallet.WalletAddress,
            T0Timestamp = _t0,
            T1Timestamp = _t1,
            T0Block = t0Block,
            T1Block = t1Block,
            AnalyzedAt = DateTime.UtcNow
        };

        var missingPriceTokens = new List<string>();
        decimal stableValueT0 = 0m;
        decimal stableValueT1 = 0m;

        try
        {
            var tokenAddresses = await _alchemy.GetTokenAddressesAsync(wallet.WalletAddress);
            var tokensToProcess = tokenAddresses.Take(10).ToList();
            analysis.TokenCount = tokensToProcess.Count + 1; // +1 for ETH

            if (tokenAddresses.Count > tokensToProcess.Count)
            {
                _logger.LogInformation("[ANALYZER] Token list truncated from {Original} to {Limited} (processing top 10)", tokenAddresses.Count, tokensToProcess.Count);
            }

            _logger.LogInformation("[ANALYZER] Token sayısı: {AddressPrefix} -> ETH + {TokenCount} (processing {ProcessedCount} tokens)",
                wallet.WalletAddress[..10], analysis.TokenCount, tokensToProcess.Count);

            var ethT0 = await _alchemy.GetEthBalanceAsync(wallet.WalletAddress, t0Block);
            var ethT1 = await _alchemy.GetEthBalanceAsync(wallet.WalletAddress, t1Block);

            var ethPriceT0 = await _priceHistory.GetEthPriceAsync(_t0);
            var ethPriceT1 = await _priceHistory.GetEthPriceAsync(_t1);

            decimal valueT0 = ethT0 * ethPriceT0;
            decimal valueT1 = ethT1 * ethPriceT1;

            foreach (var tokenAddress in tokensToProcess)
            {
                decimal amountT0 = 0m;
                decimal amountT1 = 0m;
                bool isStablecoin = _stablecoins.Contains(tokenAddress);
                int decimals = 18;

                try
                {
                    var metadata = await _alchemy.GetTokenMetadataAsync(tokenAddress);
                    if (metadata?.Decimals is int resolvedDecimals && resolvedDecimals > 0)
                    {
                        decimals = resolvedDecimals;
                    }
                    else if (!string.IsNullOrEmpty(wallet.FirstTransferToken) &&
                             wallet.FirstTransferTokenDecimals.HasValue &&
                             wallet.FirstTransferToken.Equals(tokenAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        decimals = wallet.FirstTransferTokenDecimals.Value;
                    }

                    var balanceT0 = await _alchemy.GetTokenBalanceAsync(wallet.WalletAddress, tokenAddress, t0Block);
                    var balanceT1 = await _alchemy.GetTokenBalanceAsync(wallet.WalletAddress, tokenAddress, t1Block);

                    if (balanceT0 == BigInteger.Zero && balanceT1 == BigInteger.Zero)
                    {
                        continue;
                    }

                    amountT0 = Web3.Convert.FromWei(balanceT0, decimals);
                    amountT1 = Web3.Convert.FromWei(balanceT1, decimals);

                    if (!isStablecoin && metadata?.Symbol != null)
                    {
                        var symbol = metadata.Symbol;
                        if (symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase) ||
                            symbol.Equals("USDT", StringComparison.OrdinalIgnoreCase) ||
                            symbol.Equals("USDC", StringComparison.OrdinalIgnoreCase))
                        {
                            isStablecoin = true;
                        }
                    }

                    var priceT0 = await _priceHistory.GetTokenPriceAsync(tokenAddress, _t0, isStablecoin);
                    var priceT1 = await _priceHistory.GetTokenPriceAsync(tokenAddress, _t1, isStablecoin);

                    if (priceT0 == 0 || priceT1 == 0)
                    {
                        missingPriceTokens.Add(tokenAddress);
                    }

                    valueT0 += amountT0 * priceT0;
                    valueT1 += amountT1 * priceT1;

                    if (isStablecoin)
                    {
                        stableValueT0 += amountT0 * priceT0;
                        stableValueT1 += amountT1 * priceT1;
                    }
                }
                catch (RateLimitException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ANALYZER] Token processing failed {Token}", tokenAddress);
                    missingPriceTokens.Add(tokenAddress);
                }
            }

            analysis.ValueT0Usd = valueT0;            analysis.ValueT1Usd = valueT1;

            if (valueT0 > 0)
            {
                analysis.SimpleReturn = (valueT1 - valueT0) / valueT0;
            }

            var netCashFlowUsd = await CalculateNetCashFlowAsync(wallet, t0Block, t1Block);
            analysis.NetCashFlowUsd = netCashFlowUsd;

            if (valueT0 > 0)
            {
                analysis.AdjustedReturn = (valueT1 - valueT0 - netCashFlowUsd) / valueT0;
            }
            else
            {
                analysis.AdjustedReturn = analysis.SimpleReturn;
            }

            analysis.StableHeavy = valueT0 > 0 && stableValueT0 / valueT0 >= 0.9m;
            analysis.FundingHeavy = valueT0 > 0 && Math.Abs(netCashFlowUsd) / valueT0 >= 0.5m;
            analysis.PriceMissing = missingPriceTokens.Count > 0;

            var noteParts = new List<string>();
            if (tokenAddresses.Count > tokensToProcess.Count)
            {
                noteParts.Add($"TruncatedTokens={tokenAddresses.Count - tokensToProcess.Count}");
            }
            if (missingPriceTokens.Count > 0)
            {
                noteParts.Add($"MissingPrices={string.Join(';', missingPriceTokens)}");
            }

            if (netCashFlowUsd != 0)
            {
                noteParts.Add($"NetCF={netCashFlowUsd:N2}");
            }

            analysis.Notes = noteParts.Count > 0 ? string.Join(" | ", noteParts) : null;

            _logger.LogInformation("[ANALYZER] Wallet {Address}: V0=${V0:N2}, V1=${V1:N2}, Return={Return:P2}",
                wallet.WalletAddress,
                valueT0,
                valueT1,
                analysis.SimpleReturn);

            return analysis;
        }
        catch (RateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ANALYZER] Unexpected failure while analysing {Address}", wallet.WalletAddress);
            analysis.Notes = $"Analysis failed: {ex.Message}";
            throw;
        }
    }

    private async Task<decimal> CalculateNetCashFlowAsync(CandidateWallet wallet, long t0Block, long t1Block)
    {
        try
        {
            var transfers = await _alchemy.GetAssetTransfersAsync(wallet.WalletAddress, t0Block, t1Block);
            decimal netUsd = 0m;

            foreach (var transfer in transfers)
            {
                var timestamp = transfer.BlockTimestampUtc ?? _t1;
                decimal price;

                if (string.IsNullOrEmpty(transfer.TokenAddress))
                {
                    price = await _priceHistory.GetEthPriceAsync(timestamp);
                }
                else
                {
                    var isStable = _stablecoins.Contains(transfer.TokenAddress);
                    price = await _priceHistory.GetTokenPriceAsync(transfer.TokenAddress, timestamp, isStable);
                }

                if (price <= 0)
                {
                    continue;
                }

                var usdValue = transfer.Amount * price;

                if (transfer.IsInboundFor(wallet.WalletAddress))
                {
                    netUsd += usdValue;
                }
                else if (transfer.IsOutboundFor(wallet.WalletAddress))
                {
                    netUsd -= usdValue;
                }
            }

            return netUsd;
        }
        catch (RateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ANALYZER] Cash flow calculation failed for {Address}", wallet.WalletAddress);
            return 0m;
        }
    }

    private async Task ExportResultsToCsvAsync()
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BorsaGptDbContext>();

            var results = await db.CandidateAnalysis
                .OrderByDescending(a => a.SimpleReturn ?? decimal.MinValue)
                .Take(1000)
                .Select(a => new
                {
                    a.Id,
                    a.WalletAddress,
                    SimpleReturn = a.SimpleReturn,
                    NetCashFlow = a.NetCashFlowUsd,
                    ValueT0 = a.ValueT0Usd,
                    ValueT1 = a.ValueT1Usd,
                    a.TokenCount,
                    a.AnalyzedAt
                })
                .ToListAsync();

            var csvPath = Path.Combine(AppContext.BaseDirectory, "live_pnl_results.csv");
            using var writer = new StreamWriter(csvPath, false, System.Text.Encoding.UTF8);

            await writer.WriteLineAsync("Id,WalletAddress,SimpleReturn,NetCashFlowUsd,ValueT0_USD,ValueT1_USD,TokenCount,AnalyzedAt");

            foreach (var row in results)
            {
                var simpleReturn = row.SimpleReturn?.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) ?? "";
                var netCash = row.NetCashFlow?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "";
                var valueT0 = row.ValueT0?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "";
                var valueT1 = row.ValueT1?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "";

                await writer.WriteLineAsync($"{row.Id},{row.WalletAddress},{simpleReturn},{netCash},{valueT0},{valueT1},{row.TokenCount},{row.AnalyzedAt:yyyy-MM-dd HH:mm:ss}");
            }

            _logger.LogInformation("[ANALYZER] CSV export written to {Path} ({Count} rows)", csvPath, results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ANALYZER] CSV export failed");
        }
    }
}






