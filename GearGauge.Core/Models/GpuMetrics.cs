namespace GearGauge.Core.Models;

public sealed class GpuMetrics
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public float UsagePercent { get; set; }

    public OptionalFloat TemperatureCelsius { get; set; }

    public OptionalFloat PowerWatt { get; set; }

    public OptionalFloat ClockMHz { get; set; }
}
