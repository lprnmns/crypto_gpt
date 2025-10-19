namespace BorsaGPT.Api.Models;

/// <summary>
/// Basic token metadata returned by Alchemy (decimals, symbol, name).
/// </summary>
public class TokenMetadata
{
    public string Address { get; set; } = string.Empty;
    public int? Decimals { get; set; }
    public string? Symbol { get; set; }
    public string? Name { get; set; }
    public bool? IsSpam { get; set; }
}
