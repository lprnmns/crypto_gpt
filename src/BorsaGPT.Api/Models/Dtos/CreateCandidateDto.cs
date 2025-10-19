using System.ComponentModel.DataAnnotations;

namespace BorsaGPT.Api.Models.Dtos;

/// <summary>
/// Yeni aday cüzdan oluşturmak için kullanılan DTO.
/// POST /api/candidates endpoint'inde kullanılır.
/// </summary>
public class CreateCandidateDto
{
    /// <summary>
    /// Ethereum cüzdan adresi (zorunlu, 42 karakter)
    /// </summary>
    [Required(ErrorMessage = "Cüzdan adresi zorunludur")]
    [StringLength(42, MinimumLength = 42, ErrorMessage = "Cüzdan adresi tam 42 karakter olmalı")]
    [RegularExpression(@"^0x[a-fA-F0-9]{40}$", ErrorMessage = "Geçersiz Ethereum adresi formatı")]
    public string WalletAddress { get; set; } = string.Empty;

    /// <summary>
    /// İlk transfer miktarı (ETH cinsinden, opsiyonel)
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Transfer miktarı negatif olamaz")]
    public decimal? FirstTransferAmountEth { get; set; }

    /// <summary>
    /// Token kontrat adresi (opsiyonel, 42 karakter)
    /// </summary>
    [StringLength(42, ErrorMessage = "Token adresi maksimum 42 karakter olmalı")]
    [RegularExpression(@"^0x[a-fA-F0-9]{40}$", ErrorMessage = "Geçersiz token adresi formatı")]
    public string? FirstTransferToken { get; set; }

    /// <summary>
    /// Blok numarası (zorunlu, pozitif)
    /// </summary>
    [Required(ErrorMessage = "Blok numarası zorunludur")]
    [Range(1, long.MaxValue, ErrorMessage = "Blok numarası pozitif olmalı")]
    public long BlockNumber { get; set; }
}
