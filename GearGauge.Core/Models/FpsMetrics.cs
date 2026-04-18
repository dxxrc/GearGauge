namespace GearGauge.Core.Models;

public sealed class FpsMetrics
{
    public OptionalFloat DisplayOutputFps { get; set; }

    public OptionalFloat GameFps { get; set; }

    public int? TargetDisplayIndex { get; set; }

    public string? TargetDisplayName { get; set; }

    public string? ActiveContent { get; set; }

    public string ProviderStatus { get; set; } = "NotInitialized";
}
