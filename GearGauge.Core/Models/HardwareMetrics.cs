namespace GearGauge.Core.Models;

public sealed class HardwareMetrics
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    public CpuMetrics Cpu { get; set; } = new();

    public IReadOnlyList<GpuMetrics> Gpus { get; set; } = Array.Empty<GpuMetrics>();

    public MemoryMetrics Memory { get; set; } = new();

    public FpsMetrics Fps { get; set; } = new();

    public IReadOnlyList<NetworkAdapterMetrics> NetworkAdapters { get; set; } = Array.Empty<NetworkAdapterMetrics>();

    public IReadOnlyList<MonitorInfo> ActiveMonitors { get; set; } = Array.Empty<MonitorInfo>();

    public bool IsElevated { get; set; }

    public string DataSourceInfo { get; set; } = string.Empty;
}
