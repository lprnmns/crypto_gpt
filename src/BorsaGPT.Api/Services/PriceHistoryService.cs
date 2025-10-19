using System.Net;
using System.Text.Json;
using BorsaGPT.Api.Exceptions;

namespace BorsaGPT.Api.Services;

/// <summary>
/// Provides historical price data for ETH and ERC-20 tokens using CoinGecko with DefiLlama fallback.
/// </summary>
public class PriceHistoryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PriceHistoryService> _logger;
    private readonly string _defiLlamaBaseUrl;

    // Simple cache to avoid repeated requests for the same timestamp (key = "token|yyyy-MM-dd-HH:mm").
    private readonly Dictionary<string, decimal> _priceCache = new();

    public PriceHistoryService(ILogger<PriceHistoryService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _defiLlamaBaseUrl = configuration["DefiLlama:BaseUrl"] ?? "https://coins.llama.fi";
    }

    public async Task<decimal> GetEthPriceAsync(DateTime timestamp)
    {
        return await GetCoinGeckoPriceAsync("ethereum", timestamp);
    }

    public async Task<decimal> GetTokenPriceAsync(string tokenAddress, DateTime timestamp, bool isStablecoin = false)
    {
        if (isStablecoin)
        {
            return 1.0m;
        }

        try
        {
            var price = await GetCoinGeckoTokenPriceAsync(tokenAddress, timestamp);
            if (price > 0)
            {
                return price;
            }
        }
        catch (RateLimitException ex)
        {
            _logger.LogWarning(ex, "CoinGecko rate limited for {Token}, trying DefiLlama", tokenAddress);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CoinGecko price lookup failed for {Token}, trying DefiLlama...", tokenAddress);
        }

        try
        {
            return await GetDefiLlamaPriceAsync(tokenAddress, timestamp);
        }
        catch (RateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DefiLlama price lookup failed for {Token}, returning ", tokenAddress);
            return 0m;
        }
    }

    private async Task<decimal> GetCoinGeckoPriceAsync(string coinId, DateTime timestamp)
    {
        var cacheKey = $"{coinId}|{timestamp:yyyy-MM-dd-HH:mm}";
        if (_priceCache.TryGetValue(cacheKey, out var cachedPrice))
        {
            return cachedPrice;
        }

        var from = new DateTimeOffset(timestamp.AddMinutes(-5)).ToUnixTimeSeconds();
        var to = new DateTimeOffset(timestamp.AddMinutes(5)).ToUnixTimeSeconds();
        var url = $"https://api.coingecko.com/api/v3/coins/{coinId}/market_chart/range?vs_currency=usd&from={from}&to={to}";

        var response = await _httpClient.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new RateLimitException("CoinGecko", TimeSpan.FromMinutes(5));
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var prices = doc.RootElement.GetProperty("prices");
        if (prices.GetArrayLength() == 0)
        {
            return 0m;
        }

        decimal closestPrice = 0m;
        long targetTimestamp = new DateTimeOffset(timestamp).ToUnixTimeMilliseconds();
        long minDiff = long.MaxValue;

        foreach (var pricePoint in prices.EnumerateArray())
        {
            var ts = pricePoint[0].GetInt64();
            var price = pricePoint[1].GetDecimal();
            var diff = Math.Abs(ts - targetTimestamp);

            if (diff < minDiff)
            {
                minDiff = diff;
                closestPrice = price;
            }
        }

        _priceCache[cacheKey] = closestPrice;
        return closestPrice;
    }

    private async Task<decimal> GetCoinGeckoTokenPriceAsync(string tokenAddress, DateTime timestamp)
    {
        var cacheKey = $"token:{tokenAddress}|{timestamp:yyyy-MM-dd-HH:mm}";
        if (_priceCache.TryGetValue(cacheKey, out var cachedPrice))
        {
            return cachedPrice;
        }

        var from = new DateTimeOffset(timestamp.AddMinutes(-5)).ToUnixTimeSeconds();
        var to = new DateTimeOffset(timestamp.AddMinutes(5)).ToUnixTimeSeconds();
        var url = $"https://api.coingecko.com/api/v3/coins/ethereum/contract/{tokenAddress}/market_chart/range?vs_currency=usd&from={from}&to={to}";

        var response = await _httpClient.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new RateLimitException("CoinGecko", TimeSpan.FromMinutes(5));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return 0m;
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var prices = doc.RootElement.GetProperty("prices");
        if (prices.GetArrayLength() == 0)
        {
            return 0m;
        }

        decimal closestPrice = 0m;
        long targetTimestamp = new DateTimeOffset(timestamp).ToUnixTimeMilliseconds();
        long minDiff = long.MaxValue;

        foreach (var pricePoint in prices.EnumerateArray())
        {
            var ts = pricePoint[0].GetInt64();
            var price = pricePoint[1].GetDecimal();
            var diff = Math.Abs(ts - targetTimestamp);

            if (diff < minDiff)
            {
                minDiff = diff;
                closestPrice = price;
            }
        }

        _priceCache[cacheKey] = closestPrice;
        return closestPrice;
    }

    private async Task<decimal> GetDefiLlamaPriceAsync(string tokenAddress, DateTime timestamp)
    {
        var unixTimestamp = new DateTimeOffset(timestamp).ToUnixTimeSeconds();
        var url = $"{_defiLlamaBaseUrl}/prices/historical/{unixTimestamp}/ethereum:{tokenAddress}";

        var response = await _httpClient.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new RateLimitException("DefiLlama", TimeSpan.FromMinutes(1));
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var coins = doc.RootElement.GetProperty("coins");
        var key = $"ethereum:{tokenAddress}";

        if (coins.TryGetProperty(key, out var coinData))
        {
            var price = coinData.GetProperty("price").GetDecimal();
            return price;
        }

        return 0m;
    }
}