namespace GearGauge.Core.Models;

public sealed class CpuMetrics
{
    public string ModelName { get; set; } = string.Empty;

    public float UsagePercent { get; set; }

    public OptionalFloat TemperatureCelsius { get; set; }

    public OptionalFloat PowerWatt { get; set; }

    public OptionalFloat ClockGHz { get; set; }

    public IReadOnlyList<CoreMetrics> Cores { get; set; } = Array.Empty<CoreMetrics>();
}
