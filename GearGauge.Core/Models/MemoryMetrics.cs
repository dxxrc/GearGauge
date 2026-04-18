namespace GearGauge.Core.Models;

public sealed class MemoryMetrics
{
    public string ModelName { get; set; } = string.Empty;

    public float TotalGB { get; set; }

    public float UsedGB { get; set; }

    public float AvailableGB { get; set; }

    public float UsagePercent { get; set; }
}
