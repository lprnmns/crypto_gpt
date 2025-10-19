using System.Globalization;

namespace BorsaGPT.Api.Models;

/// <summary>
/// Configuration holder for analysis time window.
/// Allows explicit start/end or relative offsets from a reference date.
/// </summary>
public class AnalysisWindowConfig
{
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }

    public DateTime? ReferenceDateUtc { get; set; }
    public double? T0OffsetHours { get; set; }
    public double? T1OffsetHours { get; set; }

    public long? T0Block { get; set; }
    public long? T1Block { get; set; }

    /// <summary>
    /// Resolve the time window based on configuration values.
    /// </summary>
    /// <param name="now">Fallback reference when offsets are used without explicit reference date.</param>
    /// <returns>Tuple containing resolved t0 and t1 timestamps in UTC.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration is insufficient.</exception>
    public (DateTime T0, DateTime T1) ResolveWindow(DateTime now)
    {
        if (StartUtc.HasValue && EndUtc.HasValue)
        {
            var t0 = DateTime.SpecifyKind(StartUtc.Value, DateTimeKind.Utc);
            var t1 = DateTime.SpecifyKind(EndUtc.Value, DateTimeKind.Utc);
            ValidateWindow(t0, t1);
            return (t0, t1);
        }

        if (ReferenceDateUtc.HasValue && T0OffsetHours.HasValue && T1OffsetHours.HasValue)
        {
            var reference = DateTime.SpecifyKind(ReferenceDateUtc.Value, DateTimeKind.Utc);
            var t0 = reference.AddHours(T0OffsetHours.Value);
            var t1 = reference.AddHours(T1OffsetHours.Value);
            ValidateWindow(t0, t1);
            return (t0, t1);
        }

        if (T0OffsetHours.HasValue && T1OffsetHours.HasValue)
        {
            var t0 = now.AddHours(T0OffsetHours.Value);
            var t1 = now.AddHours(T1OffsetHours.Value);
            ValidateWindow(t0, t1);
            return (t0, t1);
        }

        throw new InvalidOperationException("AnalysisWindow configuration is missing required values. Provide Start/End or offsets.");
    }

    public string BuildWindowKey(DateTime t0, DateTime t1) => $"{t0:O}|{t1:O}";

    public (long? T0Block, long? T1Block) GetBlockOverrides() => (T0Block, T1Block);

    private static void ValidateWindow(DateTime t0, DateTime t1)
    {
        if (t0.Kind != DateTimeKind.Utc || t1.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("Analysis window timestamps must be UTC.");
        }

        if (t0 >= t1)
        {
            throw new InvalidOperationException("Analysis window start (t0) must be earlier than end (t1).");
        }
    }
}
