namespace BorsaGPT.Api.Exceptions;

/// <summary>
/// API rate limit hatası için özel exception.
/// Alchemy, CoinGecko, Etherscan gibi servislerde 429 (Too Many Requests) alındığında fırlatılır.
/// </summary>
public class RateLimitException : Exception
{
    /// <summary>
    /// Hangi API rate limit verdi? (Alchemy, CoinGecko, Etherscan, DefiLlama)
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// Ne kadar süre beklemeli? (Retry-After header'dan gelir)
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    public RateLimitException(string provider, TimeSpan? retryAfter = null)
        : base($"Rate limit exceeded: {provider}. Retry after: {retryAfter?.TotalMinutes ?? 60} minutes")
    {
        Provider = provider;
        RetryAfter = retryAfter ?? TimeSpan.FromHours(1); // Default 1 saat
    }

    public RateLimitException(string provider, string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        Provider = provider;
        RetryAfter = retryAfter ?? TimeSpan.FromHours(1);
    }
}
