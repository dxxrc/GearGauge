using GearGauge.Core.Models;
using GearGauge.Hardware;

namespace GearGauge.Tests;

public sealed class ForegroundWindowClassifierTests
{
    [Fact]
    public void Classify_ReturnsUnknown_ForKnownBrowserVideoTitle()
    {
        var window = new ForegroundWindowInfo(
            (nint)123,
            456,
            "chrome",
            "YouTube - Video",
            1920,
            1080,
            nint.Zero);

        var kind = ForegroundWindowClassifier.Classify(window, CreateMonitors());

        Assert.Equal(ForegroundContentKind.Unknown, kind);
    }

    [Fact]
    public void Classify_ReturnsGame_ForLargeUnknownWindow()
    {
        var window = new ForegroundWindowInfo(
            (nint)123,
            456,
            "eldenring",
            "ELDEN RING",
            1920,
            1080,
            nint.Zero);

        var kind = ForegroundWindowClassifier.Classify(window, CreateMonitors());

        Assert.Equal(ForegroundContentKind.Game, kind);
    }

    [Fact]
    public void Classify_ReturnsUnknown_ForUtilityWindow()
    {
        var window = new ForegroundWindowInfo(
            (nint)123,
            456,
            "logioptionsplus",
            "Logi Options+ Settings",
            2200,
            1300,
            nint.Zero);

        var kind = ForegroundWindowClassifier.Classify(window, CreateMonitors());

        Assert.Equal(ForegroundContentKind.Unknown, kind);
    }

    [Fact]
    public void Classify_ReturnsUnknown_ForRiderWindow()
    {
        var window = new ForegroundWindowInfo(
            (nint)123,
            456,
            "rider64",
            "GearGauge - rider64",
            2200,
            1300,
            nint.Zero);

        var kind = ForegroundWindowClassifier.Classify(window, CreateMonitors());

        Assert.Equal(ForegroundContentKind.Unknown, kind);
    }

    private static IReadOnlyList<MonitorInfo> CreateMonitors()
    {
        return
        [
            new MonitorInfo
            {
                Index = 0,
                Width = 2560,
                Height = 1440,
                IsPrimary = true
            }
        ];
    }
}
