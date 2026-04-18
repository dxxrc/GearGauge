using GearGauge.Hardware;

namespace GearGauge.Tests;

public sealed class NetworkRateCalculatorTests
{
    [Fact]
    public void ToMbps_ComputesExpectedRate()
    {
        var actual = NetworkRateCalculator.ToMbps(0, 12_500_000, TimeSpan.FromSeconds(1));

        Assert.Equal(100, actual, 3);
    }

    [Fact]
    public void ToMbps_ReturnsZero_WhenElapsedIsInvalid()
    {
        var actual = NetworkRateCalculator.ToMbps(100, 200, TimeSpan.Zero);

        Assert.Equal(0, actual);
    }
}
