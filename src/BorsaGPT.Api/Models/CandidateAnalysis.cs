using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BorsaGPT.Api.Models;

/// <summary>
/// Flash crash (10 Ekim 2025) sırasında cüzdan performans analizi.
/// t0 (19:00 UTC) ve t1 (22:00 UTC) anlarındaki USD değerleri ve PnL hesaplamaları.
/// </summary>
[Table("candidate_analysis")]
public class CandidateAnalysis
{
    /// <summary>
    /// Primary key (auto-increment)
    /// </summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>
    /// İlişkili candidate_wallets tablosu ID'si
    /// </summary>
    [Column("candidate_wallet_id")]
    public long CandidateWalletId { get; set; }

    /// <summary>
    /// Cüzdan adresi (42 karakter, 0x ile başlar)
    /// </summary>
    [Required]
    [MaxLength(42)]
    [Column("wallet_address")]
    public string WalletAddress { get; set; } = string.Empty;

    // ==================== Snapshot Zamanları ====================

    /// <summary>
    /// t0 zaman damgası (UTC) — Örn: 10 Ekim 2025 19:00
    /// </summary>
    [Required]
    [Column("t0_timestamp")]
    public DateTime T0Timestamp { get; set; }

    /// <summary>
    /// t1 zaman damgası (UTC) — Örn: 10 Ekim 2025 22:00
    /// </summary>
    [Required]
    [Column("t1_timestamp")]
    public DateTime T1Timestamp { get; set; }

    /// <summary>
    /// t0 anındaki Ethereum blok numarası
    /// </summary>
    [Required]
    [Column("t0_block")]
    public long T0Block { get; set; }

    /// <summary>
    /// t1 anındaki Ethereum blok numarası
    /// </summary>
    [Required]
    [Column("t1_block")]
    public long T1Block { get; set; }

    // ==================== USD Değerler ====================

    /// <summary>
    /// t0 anındaki toplam portföy değeri (USD)
    /// ETH + tüm ERC-20 tokenların toplam USD değeri
    /// </summary>
    [Column("value_t0_usd", TypeName = "decimal(28,8)")]
    public decimal? ValueT0Usd { get; set; }

    /// <summary>
    /// t1 anındaki toplam portföy değeri (USD)
    /// </summary>
    [Column("value_t1_usd", TypeName = "decimal(28,8)")]
    public decimal? ValueT1Usd { get; set; }

    // ==================== Metrikler ====================

    /// <summary>
    /// Basit getiri oranı: (V1 - V0) / V0
    /// Örn: 0.15 = %15 kazanç, -0.30 = %30 kayıp
    /// </summary>
    [Column("simple_return", TypeName = "decimal(10,4)")]
    public decimal? SimpleReturn { get; set; }

    /// <summary>
    /// Net nakit akışı (t0-t1 arası gelen - giden USD toplamı)
    /// Pozitif = para eklendi, Negatif = para çekildi
    /// </summary>
    [Column("net_cash_flow_usd", TypeName = "decimal(28,8)")]
    public decimal? NetCashFlowUsd { get; set; }

    /// <summary>
    /// Düzeltilmiş getiri: (V1 - V0 - CF) / V0
    /// Nakit akışını temizleyerek gerçek trading performansı
    /// </summary>
    [Column("adjusted_return", TypeName = "decimal(10,4)")]
    public decimal? AdjustedReturn { get; set; }

    // ==================== Bayraklar ====================

    /// <summary>
    /// Para ekleme ağırlıklı mı? |CF| / V0 >= 0.5
    /// true ise: Cüzdan trading değil, funding ile büyümüş
    /// </summary>
    [Column("funding_heavy")]
    public bool FundingHeavy { get; set; } = false;

    /// <summary>
    /// Stablecoin ağırlıklı mı? Stable oranı >= %90
    /// true ise: Risk almayan, nakde yakın pozisyon
    /// </summary>
    [Column("stable_heavy")]
    public bool StableHeavy { get; set; } = false;

    /// <summary>
    /// Fiyat verisi eksik mi?
    /// true ise: Bazı tokenlar için fiyat bulunamadı (DefiLlama fallback veya $0)
    /// </summary>
    [Column("price_missing")]
    public bool PriceMissing { get; set; } = false;

    // ==================== Meta ====================

    /// <summary>
    /// Cüzdanda analiz edilen token sayısı (ETH dahil)
    /// </summary>
    [Column("token_count")]
    public int TokenCount { get; set; }

    /// <summary>
    /// Analiz tamamlanma zamanı (UTC)
    /// </summary>
    [Column("analyzed_at")]
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Ek notlar (error mesajları, özel durumlar)
    /// </summary>
    [Column("notes")]
    public string? Notes { get; set; }

    // ==================== Navigation Property ====================

    /// <summary>
    /// İlişkili candidate wallet (foreign key)
    /// </summary>
    [ForeignKey("CandidateWalletId")]
    public CandidateWallet? CandidateWallet { get; set; }
}
