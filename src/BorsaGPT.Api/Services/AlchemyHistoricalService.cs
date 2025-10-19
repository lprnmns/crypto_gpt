using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Numerics;
using System.Text.Json;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using BorsaGPT.Api.Exceptions;
using BorsaGPT.Api.Models;

namespace BorsaGPT.Api.Services;

/// <summary>
/// Wraps Alchemy RPC endpoints used for historical balance snapshots and token metadata lookups.
/// </summary>
public class AlchemyHistoricalService
{
    private readonly Web3 _web3;
    private readonly ILogger<AlchemyHistoricalService> _logger;
    private readonly string _rpcUrl;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, TokenMetadata> _metadataCache = new(StringComparer.OrdinalIgnoreCase);

    public AlchemyHistoricalService(
        ILogger<AlchemyHistoricalService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _rpcUrl = configuration["Alchemy:RpcUrl"] ?? throw new InvalidOperationException("Alchemy RPC URL not found");
        _web3 = new Web3(_rpcUrl);
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<decimal> GetEthBalanceAsync(string address, long blockNumber)
    {
        try
        {
            var balance = await _web3.Eth.GetBalance.SendRequestAsync(address, new HexBigInteger(blockNumber));
            var ethBalance = Web3.Convert.FromWei(balance.Value);

            _logger.LogDebug("ETH balance snapshot: {Address} @ Block #{Block} = {Balance} ETH", address, blockNumber, ethBalance);

            return ethBalance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch ETH balance for {Address} @ Block #{Block}", address, blockNumber);
            throw;
        }
    }

    public async Task<BigInteger> GetTokenBalanceAsync(string walletAddress, string tokenAddress, long blockNumber)
    {
        try
        {
            var contract = _web3.Eth.GetContract(ERC20_ABI, tokenAddress);
            var balanceOfFunction = contract.GetFunction("balanceOf");

            var blockParam = new Nethereum.RPC.Eth.DTOs.BlockParameter(new HexBigInteger(blockNumber));
            var balance = await balanceOfFunction.CallAsync<BigInteger>(blockParam, walletAddress);

            _logger.LogDebug("Token balance snapshot: {Wallet} @ {Token} Block #{Block} = {Balance} (raw)", walletAddress, tokenAddress, blockNumber, balance);

            return balance;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token balance fetch failed: {Token} @ Block #{Block}", tokenAddress, blockNumber);
            return BigInteger.Zero;
        }
    }

    public async Task<List<string>> GetTokenAddressesAsync(string walletAddress)
    {
        try
        {
            var requestBody = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "alchemy_getTokenBalances",
                @params = new object[] { walletAddress, "erc20" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_rpcUrl, content);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitException("Alchemy", TimeSpan.FromMinutes(1));
            }

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var tokenBalances = doc.RootElement.GetProperty("result").GetProperty("tokenBalances");
            var addresses = new List<string>();

            foreach (var token in tokenBalances.EnumerateArray())
            {
                var contractAddress = token.GetProperty("contractAddress").GetString();
                var tokenBalance = token.GetProperty("tokenBalance").GetString();

                if (!string.IsNullOrEmpty(contractAddress) && !string.Equals(tokenBalance, "0x0", StringComparison.OrdinalIgnoreCase) && !string.Equals(tokenBalance, "0x", StringComparison.OrdinalIgnoreCase))
                {
                    addresses.Add(contractAddress);
                }
            }

            _logger.LogInformation("Alchemy token inventory: {Wallet} -> {Count} tokens", walletAddress, addresses.Count);

            return addresses;
        }
        catch (RateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch token inventory for {Wallet}", walletAddress);
            return new List<string>();
        }
    }

    public async Task<TokenMetadata?> GetTokenMetadataAsync(string tokenAddress)
    {
        if (string.IsNullOrWhiteSpace(tokenAddress))
        {
            return null;
        }

        if (_metadataCache.TryGetValue(tokenAddress, out var cached))
        {
            return cached;
        }

        var requestBody = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "alchemy_getTokenMetadata",
            @params = new object[] { tokenAddress }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(_rpcUrl, content);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitException("Alchemy", TimeSpan.FromMinutes(1));
            }

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);

            var result = doc.RootElement.GetProperty("result");
            var metadata = new TokenMetadata
            {
                Address = tokenAddress,
                Name = result.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null,
                Symbol = result.TryGetProperty("symbol", out var symbolProp) ? symbolProp.GetString() : null,
                Decimals = TryParseInt(result, "decimals")
            };

            _metadataCache[tokenAddress] = metadata;
            _logger.LogDebug("Token metadata cached: {Token} (decimals={Decimals}, symbol={Symbol})", tokenAddress, metadata.Decimals, metadata.Symbol);

            return metadata;
        }
        catch (RateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token metadata lookup failed for {Token}", tokenAddress);
            return null;
        }
    }
    public async Task<List<AssetTransfer>> GetAssetTransfersAsync(string walletAddress, long fromBlock, long toBlock)
    {
        var transfers = new List<AssetTransfer>();

        if (string.IsNullOrWhiteSpace(walletAddress))
        {
            return transfers;
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        async Task FetchAsync(string directionKey)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["fromBlock"] = $"0x{fromBlock:X}",
                ["toBlock"] = $"0x{toBlock:X}",
                ["withMetadata"] = true,
                ["excludeZeroValue"] = true,
                ["category"] = new object[] { "external", "erc20" },
                ["maxCount"] = "0x3e8",
                [directionKey] = walletAddress
            };

            var request = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "alchemy_getAssetTransfers",
                @params = new object[] { parameters }
            };

            using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_rpcUrl, content);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitException("Alchemy", TimeSpan.FromMinutes(1));
            }

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);

            if (!doc.RootElement.TryGetProperty("result", out var resultElement) ||
                !resultElement.TryGetProperty("transfers", out var transfersElement))
            {
                return;
            }

            foreach (var transfer in transfersElement.EnumerateArray())
            {
                var uniqueId = transfer.TryGetProperty("uniqueId", out var idProp) ? idProp.GetString() : null;
                if (!string.IsNullOrEmpty(uniqueId) && !seenIds.Add(uniqueId))
                {
                    continue;
                }

                var rawContract = transfer.TryGetProperty("rawContract", out var rawContractProp) ? rawContractProp : default;
                var decimals = TryParseInt(rawContract, "decimals") ?? 18;
                var amount = ParseTransferAmount(transfer, rawContract, decimals);
                if (amount <= 0)
                {
                    continue;
                }

                transfers.Add(new AssetTransfer
                {
                    From = transfer.TryGetProperty("from", out var fromProp) ? fromProp.GetString() ?? string.Empty : string.Empty,
                    To = transfer.TryGetProperty("to", out var toProp) ? toProp.GetString() ?? string.Empty : string.Empty,
                    TokenAddress = rawContract.TryGetProperty("address", out var addressProp) ? addressProp.GetString() : null,
                    Symbol = transfer.TryGetProperty("asset", out var assetProp) ? assetProp.GetString() : null,
                    Decimals = decimals,
                    Amount = amount,
                    BlockTimestampUtc = TryParseTimestamp(transfer)
                });
            }
        }

        try
        {
            await FetchAsync("fromAddress");
            await FetchAsync("toAddress");
        }
        catch (RateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Asset transfer lookup failed for wallet {Wallet}", walletAddress);
        }

        return transfers;
    }

    private static decimal ParseTransferAmount(JsonElement transfer, JsonElement rawContract, int decimals)
    {
        if (transfer.TryGetProperty("value", out var valueProp) &&
            decimal.TryParse(valueProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return parsedValue;
        }

        if (rawContract.ValueKind == JsonValueKind.Object && rawContract.TryGetProperty("value", out var rawValueProp))
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

    private static DateTime? TryParseTimestamp(JsonElement transfer)
    {
        if (transfer.TryGetProperty("metadata", out var metadata) &&
            metadata.TryGetProperty("blockTimestamp", out var timestampProp))
        {
            var timestamp = timestampProp.GetString();
            if (!string.IsNullOrEmpty(timestamp) &&
                DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return parsed;
            }
        }

        return null;
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

    private const string ERC20_ABI = @"[
        {""constant"":true,""inputs"":[{""name"":""_owner"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""name"":""balance"",""type"":""uint256""}],""type"":""function""},
        {""constant"":true,""inputs"":[],""name"":""decimals"",""outputs"":[{""name"":"""",""type"":""uint8""}],""type"":""function""},
        {""constant"":true,""inputs"":[],""name"":""symbol"",""outputs"":[{""name"":"""",""type"":""string""}],""type"":""function""}
    ]";
}



