using GearGauge.Core.Models;
using GearGauge.Hardware;

namespace GearGauge.Tests;

public sealed class FpsMonitorServiceTests
{
    [Fact]
    public void ComposeProviderStatus_SkipsEmptySegments()
    {
        var actual = FpsMonitorService.ComposeProviderStatus(
        [
            "DXGI-Dup(ok)",
            "",
            "Non-admin",
            "  ",
            "Game=eldenring"
        ]);

        Assert.Equal("DXGI-Dup(ok) | Non-admin | Game=eldenring", actual);
    }

    [Fact]
    public void SelectBestContentFps_PrefersPerProcessPresentRate()
    {
        var actual = FpsMonitorService.SelectBestContentFps(118.5f, 60f);

        Assert.True(actual.HasValue);
        Assert.Equal(118.5f, actual.Value!.Value, 3);
    }

    [Fact]
    public void SelectBestContentFps_FallsBackToDwmWhenProcessRateIsMissing()
    {
        var actual = FpsMonitorService.SelectBestContentFps(OptionalFloat.None, 72f);

        Assert.True(actual.HasValue);
        Assert.Equal(72f, actual.Value!.Value, 3);
    }

    [Fact]
    public void IsMeaningfulForegroundGameCandidate_RejectsPureDwmAnimation()
    {
        var actual = FpsMonitorService.IsMeaningfulForegroundGameCandidate(OptionalFloat.None, 120f, 0.8f);

        Assert.False(actual);
    }

    [Fact]
    public void IsMeaningfulForegroundGameCandidate_AcceptsEtwOrGpuEvidence()
    {
        Assert.True(FpsMonitorService.IsMeaningfulForegroundGameCandidate(90f, OptionalFloat.None, 0f));
        Assert.True(FpsMonitorService.IsMeaningfulForegroundGameCandidate(OptionalFloat.None, 60f, 12f));
    }
}
