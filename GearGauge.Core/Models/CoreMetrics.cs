namespace GearGauge.Core.Models;

public sealed class CoreMetrics
{
    public int Index { get; set; }

    public float UsagePercent { get; set; }

    public OptionalFloat ClockGHz { get; set; }

    public OptionalFloat TemperatureCelsius { get; set; }

    public OptionalFloat PowerWatt { get; set; }
}
