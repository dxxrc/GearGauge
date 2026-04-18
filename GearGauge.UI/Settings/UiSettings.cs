namespace GearGauge.UI.Settings;

public sealed class UiSettings
{
    public const string AllDevicesValue = "__all__";

    public int SampleIntervalMs { get; set; } = 1000;

    public string Language { get; set; } = LanguageModes.System;

    public string ThemeMode { get; set; } = ThemeModes.System;

    public int CpuUsageDecimals { get; set; } = 2;

    public int GpuUsageDecimals { get; set; } = 2;

    public int MemoryUsageDecimals { get; set; } = 2;

    public int CpuTemperatureDecimals { get; set; } = 1;

    public int GpuTemperatureDecimals { get; set; } = 1;

    public int CpuPowerDecimals { get; set; } = 1;

    public int GpuPowerDecimals { get; set; } = 1;

    public string CpuClockUnit { get; set; } = FrequencyUnit.GHz;

    public int CpuClockDecimals { get; set; } = 2;

    public string GpuClockUnit { get; set; } = FrequencyUnit.MHz;

    public int GpuClockDecimals { get; set; } = 0;

    public int MemoryCapacityDecimals { get; set; } = 2;

    public string NetworkSpeedUnit { get; set; } = NetworkSpeedUnits.Mbps;

    public int NetworkSpeedDecimals { get; set; } = 3;

    public string TemperatureUnit { get; set; } = TemperatureUnits.Celsius;

    public string SelectedGpuId { get; set; } = AllDevicesValue;

    public string SelectedNetworkAdapterId { get; set; } = AllDevicesValue;

    public string MetricGpuId { get; set; } = AllDevicesValue;

    public string MetricNetworkAdapterId { get; set; } = AllDevicesValue;

    public bool AutoStart { get; set; }

    public bool CloseToTray { get; set; }

    // --- Overlay settings ---
    public bool OverlayEnabled { get; set; }

    public string OverlayEdge { get; set; } = "Top";

    public string OverlayAlignment { get; set; } = "Start";

    public string OverlayPalette { get; set; } = OverlayPaletteNames.NeonCyber;

    public int OverlayItemSpacing { get; set; } = 6;

    public int OverlayFontSize { get; set; } = 13;

    public bool OverlayBackgroundEnabled { get; set; } = true;

    public string OverlayBackgroundColor { get; set; } = "#000000";

    public double OverlayBackgroundOpacity { get; set; } = 0.3;

    public List<OverlayMetricConfig> OverlayMetrics { get; set; } = OverlayMetricConfig.CreateDefaults();

    // --- Taskbar Widget settings ---
    public bool TaskbarWidgetEnabled { get; set; }

    public string TaskbarPalette { get; set; } = OverlayPaletteNames.NeonCyber;

    public int TaskbarFontSize { get; set; } = 11;

    public int TaskbarRowSpacing { get; set; } = 2;

    public int TaskbarMaxRows { get; set; } = 2;

    public string TaskbarPosition { get; set; } = TaskbarPositionNames.LeftOfTray;

    public List<OverlayMetricConfig> TaskbarMetrics { get; set; } = OverlayMetricConfig.CreateDefaults();

    public UiSettings Normalize()
    {
        SampleIntervalMs = Clamp(SampleIntervalMs, 200, 5000);
        CpuUsageDecimals = Clamp(CpuUsageDecimals, 0, 4);
        GpuUsageDecimals = Clamp(GpuUsageDecimals, 0, 4);
        MemoryUsageDecimals = Clamp(MemoryUsageDecimals, 0, 4);
        CpuTemperatureDecimals = Clamp(CpuTemperatureDecimals, 0, 3);
        GpuTemperatureDecimals = Clamp(GpuTemperatureDecimals, 0, 3);
        CpuPowerDecimals = Clamp(CpuPowerDecimals, 0, 3);
        GpuPowerDecimals = Clamp(GpuPowerDecimals, 0, 3);
        CpuClockDecimals = Clamp(CpuClockDecimals, 0, 4);
        GpuClockDecimals = Clamp(GpuClockDecimals, 0, 4);
        MemoryCapacityDecimals = Clamp(MemoryCapacityDecimals, 0, 4);
        NetworkSpeedDecimals = Clamp(NetworkSpeedDecimals, 0, 4);

        if (!FrequencyUnit.IsValid(CpuClockUnit))
        {
            CpuClockUnit = FrequencyUnit.GHz;
        }

        if (!FrequencyUnit.IsValid(GpuClockUnit))
        {
            GpuClockUnit = FrequencyUnit.MHz;
        }

        if (!LanguageModes.IsValid(Language))
        {
            Language = LanguageModes.System;
        }

        if (!ThemeModes.IsValid(ThemeMode))
        {
            ThemeMode = ThemeModes.System;
        }

        if (!NetworkSpeedUnits.IsValid(NetworkSpeedUnit))
        {
            NetworkSpeedUnit = NetworkSpeedUnits.Mbps;
        }

        if (!TemperatureUnits.IsValid(TemperatureUnit))
        {
            TemperatureUnit = TemperatureUnits.Celsius;
        }

        SelectedGpuId = string.IsNullOrWhiteSpace(SelectedGpuId) ? AllDevicesValue : SelectedGpuId;
        SelectedNetworkAdapterId = string.IsNullOrWhiteSpace(SelectedNetworkAdapterId) ? AllDevicesValue : SelectedNetworkAdapterId;
        MetricGpuId = string.IsNullOrWhiteSpace(MetricGpuId) ? AllDevicesValue : MetricGpuId;
        MetricNetworkAdapterId = string.IsNullOrWhiteSpace(MetricNetworkAdapterId) ? AllDevicesValue : MetricNetworkAdapterId;

        if (!global::GearGauge.UI.Settings.OverlayEdge.IsValid(OverlayEdge))
            OverlayEdge = "Top";

        if (!global::GearGauge.UI.Settings.OverlayAlignment.IsValid(OverlayAlignment))
            OverlayAlignment = "Start";

        if (!OverlayPaletteNames.IsValid(OverlayPalette))
            OverlayPalette = OverlayPaletteNames.NeonCyber;

        OverlayItemSpacing = Clamp(OverlayItemSpacing, 0, 40);
        OverlayFontSize = Clamp(OverlayFontSize, 8, 36);
        OverlayBackgroundOpacity = Math.Min(1.0, Math.Max(0.0, OverlayBackgroundOpacity));

        if (OverlayMetrics is null || OverlayMetrics.Count == 0)
            OverlayMetrics = OverlayMetricConfig.CreateDefaults();

        // Taskbar widget normalization
        if (!OverlayPaletteNames.IsValid(TaskbarPalette))
            TaskbarPalette = OverlayPaletteNames.NeonCyber;

        TaskbarFontSize = Clamp(TaskbarFontSize, 8, 20);
        TaskbarRowSpacing = Clamp(TaskbarRowSpacing, 0, 10);
        TaskbarMaxRows = Clamp(TaskbarMaxRows, 1, 4);

        if (!TaskbarPositionNames.IsValid(TaskbarPosition))
            TaskbarPosition = TaskbarPositionNames.LeftOfTray;

        if (TaskbarMetrics is null || TaskbarMetrics.Count == 0)
            TaskbarMetrics = OverlayMetricConfig.CreateDefaults();

        return this;
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(max, Math.Max(min, value));
    }
}

public static class LanguageModes
{
    public const string System = "system";
    public const string Chinese = "zh-CN";
    public const string English = "en-US";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, System, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Chinese, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, English, StringComparison.OrdinalIgnoreCase);
    }
}

public static class ThemeModes
{
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, System, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Light, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Dark, StringComparison.OrdinalIgnoreCase);
    }
}

public static class FrequencyUnit
{
    public const string GHz = "GHz";
    public const string MHz = "MHz";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, GHz, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, MHz, StringComparison.OrdinalIgnoreCase);
    }
}

public static class TemperatureUnits
{
    public const string Celsius = "celsius";
    public const string Fahrenheit = "fahrenheit";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, Celsius, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Fahrenheit, StringComparison.OrdinalIgnoreCase);
    }
}

public static class NetworkSpeedUnits
{
    public const string Kbps = "Kbps";
    public const string Mbps = "Mbps";
    public const string Gbps = "Gbps";
    public const string KBps = "KB/s";
    public const string MBps = "MB/s";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, Kbps, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Mbps, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Gbps, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, KBps, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, MBps, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class OverlayMetricConfig
{
    public string MetricKey { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public bool ShowIcon { get; set; } = true;
    public bool ShowLabel { get; set; } = true;
    public string DisplayColor { get; set; } = "#00FFCC";
    public string CustomLabel { get; set; } = string.Empty;

    public static List<OverlayMetricConfig> CreateDefaults()
    {
        var palette = SciFiPalettes.Get(OverlayPaletteNames.NeonCyber);
        return OverlayMetricKeys.All.Select(key => new OverlayMetricConfig
        {
            MetricKey = key,
            IsVisible = true,
            ShowIcon = true,
            ShowLabel = true,
            DisplayColor = palette.TryGetValue(key, out var color) ? color : "#00FFCC",
            CustomLabel = string.Empty
        }).ToList();
    }
}
