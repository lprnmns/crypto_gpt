namespace BorsaGPT.Api.Models;

/// <summary>
/// Checkpoint information for long running wallet analysis jobs.
/// Stored inside progress.json so the pipeline can resume after interruptions.
/// </summary>
public class AnalysisProgress
{
    public long LastProcessedWalletId { get; set; }
    public string LastProcessedAddress { get; set; } = string.Empty;
    public int TotalWallets { get; set; }
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Identifier for the active analysis time window (t0|t1) used to invalidate stale checkpoints.
    /// </summary>
    public string AnalysisWindowKey { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }
    public DateTime LastCheckpoint { get; set; }
    public bool RateLimitHit { get; set; }
    public string? RateLimitProvider { get; set; }
    public DateTime? NextRetryAfter { get; set; }
    public bool IsCompleted { get; set; }
    public string? ErrorMessage { get; set; }
}
