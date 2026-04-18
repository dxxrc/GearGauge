namespace GearGauge.Core.Models;

public sealed class MonitorInfo
{
    public int Index { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string FriendlyName { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public int RefreshRate { get; set; }

    public bool IsPrimary { get; set; }
}
