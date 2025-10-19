namespace BorsaGPT.Api.Models;

/// <summary>
/// Lightweight DTO representing an asset transfer returned by Alchemy.
/// </summary>
public class AssetTransfer
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string? TokenAddress { get; set; }
    public string? Symbol { get; set; }
    public int? Decimals { get; set; }
    public decimal Amount { get; set; }
    public DateTime? BlockTimestampUtc { get; set; }

    public bool IsInboundFor(string walletAddress) =>
        !string.IsNullOrEmpty(walletAddress) &&
        string.Equals(To, walletAddress, StringComparison.OrdinalIgnoreCase);

    public bool IsOutboundFor(string walletAddress) =>
        !string.IsNullOrEmpty(walletAddress) &&
        string.Equals(From, walletAddress, StringComparison.OrdinalIgnoreCase);
}
