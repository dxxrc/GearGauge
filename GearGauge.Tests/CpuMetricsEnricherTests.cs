using GearGauge.Core.Models;
using GearGauge.Hardware;

namespace GearGauge.Tests;

public sealed class CpuMetricsEnricherTests
{
    [Fact]
    public void ApplyEstimatedPowerFallback_DistributesPackagePowerByCoreUsage()
    {
        CoreMetrics[] cores =
        [
            new CoreMetrics { Index = 1, UsagePercent = 75 },
            new CoreMetrics { Index = 2, UsagePercent = 25 }
        ];

        CpuMetricsEnricher.ApplyEstimatedPowerFallback(cores, 80f);

        Assert.True(cores[0].PowerWatt.HasValue);
        Assert.True(cores[1].PowerWatt.HasValue);
        Assert.InRange(cores[0].PowerWatt.Value!.Value, 59.99f, 60.01f);
        Assert.InRange(cores[1].PowerWatt.Value!.Value, 19.99f, 20.01f);
    }

    [Fact]
    public void ApplySharedTemperatureFallback_FillsMissingCoreTemperatures()
    {
        CoreMetrics[] cores =
        [
            new CoreMetrics { Index = 1, TemperatureCelsius = OptionalFloat.None },
            new CoreMetrics { Index = 2, TemperatureCelsius = 55f }
        ];

        CpuMetricsEnricher.ApplySharedTemperatureFallback(cores, 62.5f);

        Assert.True(cores[0].TemperatureCelsius.HasValue);
        Assert.True(cores[1].TemperatureCelsius.HasValue);
        Assert.InRange(cores[0].TemperatureCelsius.Value!.Value, 62.49f, 62.51f);
        Assert.InRange(cores[1].TemperatureCelsius.Value!.Value, 54.99f, 55.01f);
    }
}
