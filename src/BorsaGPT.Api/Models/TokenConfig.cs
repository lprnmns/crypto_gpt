namespace BorsaGPT.Api.Models;

/// <summary>
/// appsettings.json'dan okunan token konfigürasyonu
/// </summary>
public class TokenConfig
{
    /// <summary>
    /// Token sembolü (örn: USDT, WETH)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Ethereum contract adresi (0x ile başlar, 42 karakter)
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Token ondalık basamak sayısı (6 = USDT/USDC, 18 = çoğu token)
    /// </summary>
    public int Decimals { get; set; }

    /// <summary>
    /// Stablecoin flag → true ise fiyat $1 kabul edilir (API çağrısı yapılmaz)
    /// </summary>
    public bool IsStablecoin { get; set; }
}
