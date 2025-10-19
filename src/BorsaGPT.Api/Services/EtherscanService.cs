using System.Collections.Concurrent;
using System.Text.Json;
using BorsaGPT.Api.Exceptions;

namespace BorsaGPT.Api.Services;

/// <summary>
/// Thin wrapper around the Etherscan block-by-time endpoint with client-side caching.
/// </summary>
public class EtherscanService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EtherscanService> _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ConcurrentDictionary<string, long> _blockCache = new();

    public EtherscanService(ILogger<EtherscanService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = configuration["Etherscan:ApiKey"] ?? throw new InvalidOperationException("Etherscan API key not found");
        _baseUrl = configuration["Etherscan:BaseUrl"] ?? "https://api.etherscan.io/api";
    }

    /// <summary>
    /// Resolve the closest Ethereum block number for a given UTC timestamp.
    /// </summary>
    public async Task<long> GetBlockNumberByTimestampAsync(DateTime timestamp, string closest = "before")
    {
        var unixTimestamp = new DateTimeOffset(timestamp).ToUnixTimeSeconds();
        var cacheKey = $"{closest}:{unixTimestamp}";

        if (_blockCache.TryGetValue(cacheKey, out var cachedBlock))
        {
            _logger.LogDebug("Etherscan block cache hit: {Timestamp} ({Closest}) -> #{Block}", timestamp, closest, cachedBlock);
            return cachedBlock;
        }

        var url = $"{_baseUrl}?chainid=1&module=block&action=getblocknobytime&timestamp={unixTimestamp}&closest={closest}&apikey={_apiKey}";

        try
        {
            _logger.LogDebug("Etherscan request: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Etherscan rate limit hit.");
                throw new RateLimitException("Etherscan", TimeSpan.FromSeconds(5));
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            var status = root.GetProperty("status").GetString();
            if (!string.Equals(status, "1", StringComparison.Ordinal))
            {
                var message = root.GetProperty("message").GetString();
                throw new Exception($"Etherscan API error: {message}");
            }

            var blockNumberStr = root.GetProperty("result").GetString();
            var blockNumber = long.Parse(blockNumberStr!);

            _blockCache[cacheKey] = blockNumber;
            _logger.LogInformation("Etherscan V2: {Timestamp} ({Closest}) -> Block #{BlockNumber}", timestamp, closest, blockNumber);

            return blockNumber;
        }
        catch (RateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Etherscan block lookup failed for {Timestamp} ({Closest})", timestamp, closest);
            throw;
        }
    }

    /// <summary>
    /// Convenience helper to fetch both start and end blocks for an analysis window.
    /// </summary>
    public async Task<(long t0Block, long t1Block)> GetBlockRangeAsync(DateTime t0, DateTime t1)
    {
        var t0Block = await GetBlockNumberByTimestampAsync(t0, "before");
        await Task.Delay(500); // soften rate limits

        var t1Block = await GetBlockNumberByTimestampAsync(t1, "after");

        _logger.LogInformation("Etherscan block window: {T0} (#{T0Block}) -> {T1} (#{T1Block})", t0, t0Block, t1, t1Block);

        return (t0Block, t1Block);
    }
}
