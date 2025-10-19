using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using BorsaGPT.Api.Exceptions;

namespace BorsaGPT.Api.Services;

/// <summary>
/// Retrieves spot prices from CoinGecko with a short-lived cache.
/// Stablecoins always return  to avoid unnecessary API calls.
/// </summary>
public class TokenPriceService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TokenPriceService> _logger;

    private readonly ConcurrentDictionary<string, (decimal Price, DateTime CachedAt)> _priceCache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public TokenPriceService(ILogger<TokenPriceService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<decimal> GetPriceAsync(string symbol, bool isStablecoin)
    {
        if (isStablecoin)
        {
            _logger.LogDebug("{Symbol} stablecoin, fiyat = ", symbol);
            return 1m;
        }

        if (_priceCache.TryGetValue(symbol, out var cached))
        {
            var age = DateTime.UtcNow - cached.CachedAt;
            if (age < _cacheDuration)
            {
                _logger.LogDebug("{Symbol} cache'den alındı:  (yaş: {Age})", symbol, cached.Price, age);
                return cached.Price;
            }
        }

        try
        {
            var coinId = GetCoinGeckoId(symbol);
            var response = await _httpClient.GetAsync($"simple/price?ids={coinId}&vs_currencies=usd");

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitException("CoinGecko", TimeSpan.FromMinutes(5));
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var price = doc.RootElement.GetProperty(coinId).GetProperty("usd").GetDecimal();

            _priceCache[symbol] = (price, DateTime.UtcNow);
            _logger.LogInformation("{Symbol} fiyatı güncellendi: ", symbol, price);
            return price;
        }
        catch (RateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Symbol} fiyatı alınamadı, fallback = ", symbol);
            return 0m;
        }
    }

    private string GetCoinGeckoId(string symbol)
    {
        return symbol.ToUpperInvariant() switch
        {
            "WETH" => "weth",
            "WBTC" => "wrapped-bitcoin",
            "LINK" => "chainlink",
            "UNI" => "uniswap",
            "SHIB" => "shiba-inu",
            _ => symbol.ToLowerInvariant()
        };
    }
}