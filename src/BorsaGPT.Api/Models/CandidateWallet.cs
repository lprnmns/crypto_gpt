using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BorsaGPT.Api.Models;

/// <summary>
/// Snapshot of candidate wallets detected by the spider (large transfers, whales, etc.).
/// </summary>
[Table("candidate_wallets")]
public class CandidateWallet
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [MaxLength(42)]
    [Column("wallet_address")]
    public string WalletAddress { get; set; } = string.Empty;

    [Required]
    [Column("detected_at")]
    public DateTime DetectedAt { get; set; }

    [Column("first_transfer_amount_eth", TypeName = "decimal(28,18)")]
    public decimal? FirstTransferAmountEth { get; set; }

    [MaxLength(42)]
    [Column("first_transfer_token")]
    public string? FirstTransferToken { get; set; }

    [Column("first_transfer_token_decimals")]
    public int? FirstTransferTokenDecimals { get; set; }

    [Required]
    [Column("block_number")]
    public long BlockNumber { get; set; }

    [Column("analyzed")]
    public bool Analyzed { get; set; } = false;
}
