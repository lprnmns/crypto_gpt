using BorsaGPT.Api.Models;

namespace BorsaGPT.Tests;

public class AnalysisWindowConfigTests
{
    [Fact]
    public void ResolveWindow_UsesExplicitStartAndEnd()
    {
        var config = new AnalysisWindowConfig
        {
            StartUtc = new DateTime(2025, 10, 10, 19, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2025, 10, 10, 22, 0, 0, DateTimeKind.Utc)
        };

        var (t0, t1) = config.ResolveWindow(DateTime.UtcNow);

        Assert.Equal(config.StartUtc, t0);
        Assert.Equal(config.EndUtc, t1);
    }

    [Fact]
    public void ResolveWindow_UsesOffsets_WhenExplicitRangeMissing()
    {
        var now = new DateTime(2025, 10, 9, 0, 0, 0, DateTimeKind.Utc);
        var reference = new DateTime(2025, 10, 10, 00, 00, 00, DateTimeKind.Utc);

        var config = new AnalysisWindowConfig
        {
            ReferenceDateUtc = reference,
            T0OffsetHours = -5,
            T1OffsetHours = -2
        };

        var (t0, t1) = config.ResolveWindow(now);

        Assert.Equal(reference.AddHours(-5), t0);
        Assert.Equal(reference.AddHours(-2), t1);
    }

    [Fact]
    public void BuildWindowKey_ProducesStableFormat()
    {
        var config = new AnalysisWindowConfig();
        var t0 = new DateTime(2025, 10, 10, 19, 0, 0, DateTimeKind.Utc);
        var t1 = new DateTime(2025, 10, 10, 22, 0, 0, DateTimeKind.Utc);

        var key = config.BuildWindowKey(t0, t1);

        Assert.Equal("2025-10-10T19:00:00.0000000Z|2025-10-10T22:00:00.0000000Z", key);
    }
}
