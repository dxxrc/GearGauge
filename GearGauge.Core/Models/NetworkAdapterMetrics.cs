namespace GearGauge.Core.Models;

public sealed class NetworkAdapterMetrics
{
    public string AdapterId { get; set; } = string.Empty;

    public string AdapterName { get; set; } = string.Empty;

    public bool IsUp { get; set; }

    public bool IsSelected { get; set; }

    public double UploadMbps { get; set; }

    public double DownloadMbps { get; set; }

    public string Type { get; set; } = string.Empty;
}
