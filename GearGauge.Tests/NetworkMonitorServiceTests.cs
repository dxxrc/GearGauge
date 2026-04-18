using GearGauge.Hardware;

namespace GearGauge.Tests;

public sealed class NetworkMonitorServiceTests
{
    [Fact]
    public void GetStaleSampleIds_ReturnsOnlyMissingAdapters()
    {
        var actual = NetworkMonitorService.GetStaleSampleIds(
            ["eth0", "wifi0", "vpn0"],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eth0", "wifi0" });

        Assert.Equal(["vpn0"], actual);
    }
}
