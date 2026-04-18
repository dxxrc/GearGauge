using GearGauge.UI.Settings;

namespace GearGauge.UI.ViewModels;

public sealed class UiSettingsViewModel : ObservableViewModel
{
    public event Action? Changed;

    public UiSettingsViewModel()
    {
        PropertyChanged += (_, _) => Changed?.Invoke();
    }

    private int _sampleIntervalMs;
    private string _language = LanguageModes.System;
    private string _themeMode = ThemeModes.System;
    private int _cpuUsageDecimals;
    private int _gpuUsageDecimals;
    private int _memoryUsageDecimals;
    private int _cpuTemperatureDecimals;
    private int _gpuTemperatureDecimals;
    private int _cpuPowerDecimals;
    private int _gpuPowerDecimals;
    private string _cpuClockUnit = FrequencyUnit.GHz;
    private int _cpuClockDecimals;
    private string _gpuClockUnit = FrequencyUnit.MHz;
    private int _gpuClockDecimals;
    private int _memoryCapacityDecimals;
    private string _networkSpeedUnit = NetworkSpeedUnits.Mbps;
    private int _networkSpeedDecimals;
    private string _temperatureUnit = TemperatureUnits.Celsius;
    private string _selectedGpuId = UiSettings.AllDevicesValue;
    private string _selectedNetworkAdapterId = UiSettings.AllDevicesValue;
    private string _metricGpuId = UiSettings.AllDevicesValue;
    private string _metricNetworkAdapterId = UiSettings.AllDevicesValue;
    private bool _autoStart;
    private bool _closeToTray;
    private bool _overlayEnabled;
    private string _overlayEdge = Settings.OverlayEdge.Top;
    private string _overlayAlignment = Settings.OverlayAlignment.Start;
    private string _overlayPalette = Settings.OverlayPaletteNames.NeonCyber;
    private int _overlayItemSpacing = 6;
    private int _overlayFontSize = 13;
    private bool _overlayBackgroundEnabled = true;
    private string _overlayBackgroundColor = "#000000";
    private double _overlayBackgroundOpacity = 0.3;

    public int SampleIntervalMs
    {
        get => _sampleIntervalMs;
        set => SetProperty(ref _sampleIntervalMs, value);
    }

    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    public string ThemeMode
    {
        get => _themeMode;
        set => SetProperty(ref _themeMode, value);
    }

    public int CpuUsageDecimals
    {
        get => _cpuUsageDecimals;
        set => SetProperty(ref _cpuUsageDecimals, value);
    }

    public int GpuUsageDecimals
    {
        get => _gpuUsageDecimals;
        set => SetProperty(ref _gpuUsageDecimals, value);
    }

    public int MemoryUsageDecimals
    {
        get => _memoryUsageDecimals;
        set => SetProperty(ref _memoryUsageDecimals, value);
    }

    public int CpuTemperatureDecimals
    {
        get => _cpuTemperatureDecimals;
        set => SetProperty(ref _cpuTemperatureDecimals, value);
    }

    public int GpuTemperatureDecimals
    {
        get => _gpuTemperatureDecimals;
        set => SetProperty(ref _gpuTemperatureDecimals, value);
    }

    public int CpuPowerDecimals
    {
        get => _cpuPowerDecimals;
        set => SetProperty(ref _cpuPowerDecimals, value);
    }

    public int GpuPowerDecimals
    {
        get => _gpuPowerDecimals;
        set => SetProperty(ref _gpuPowerDecimals, value);
    }

    public string CpuClockUnit
    {
        get => _cpuClockUnit;
        set => SetProperty(ref _cpuClockUnit, value);
    }

    public int CpuClockDecimals
    {
        get => _cpuClockDecimals;
        set => SetProperty(ref _cpuClockDecimals, value);
    }

    public string GpuClockUnit
    {
        get => _gpuClockUnit;
        set => SetProperty(ref _gpuClockUnit, value);
    }

    public int GpuClockDecimals
    {
        get => _gpuClockDecimals;
        set => SetProperty(ref _gpuClockDecimals, value);
    }

    public int MemoryCapacityDecimals
    {
        get => _memoryCapacityDecimals;
        set => SetProperty(ref _memoryCapacityDecimals, value);
    }

    public string NetworkSpeedUnit
    {
        get => _networkSpeedUnit;
        set => SetProperty(ref _networkSpeedUnit, value);
    }

    public int NetworkSpeedDecimals
    {
        get => _networkSpeedDecimals;
        set => SetProperty(ref _networkSpeedDecimals, value);
    }

    public string TemperatureUnit
    {
        get => _temperatureUnit;
        set => SetProperty(ref _temperatureUnit, value);
    }

    public string SelectedGpuId
    {
        get => _selectedGpuId;
        set => SetProperty(ref _selectedGpuId, value);
    }

    public string SelectedNetworkAdapterId
    {
        get => _selectedNetworkAdapterId;
        set => SetProperty(ref _selectedNetworkAdapterId, value);
    }

    public string MetricGpuId
    {
        get => _metricGpuId;
        set => SetProperty(ref _metricGpuId, value);
    }

    public string MetricNetworkAdapterId
    {
        get => _metricNetworkAdapterId;
        set => SetProperty(ref _metricNetworkAdapterId, value);
    }

    public bool AutoStart
    {
        get => _autoStart;
        set => SetProperty(ref _autoStart, value);
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set => SetProperty(ref _closeToTray, value);
    }

    public bool OverlayEnabled
    {
        get => _overlayEnabled;
        set => SetProperty(ref _overlayEnabled, value);
    }

    public string OverlayEdge
    {
        get => _overlayEdge;
        set => SetProperty(ref _overlayEdge, value);
    }

    public string OverlayAlignment
    {
        get => _overlayAlignment;
        set => SetProperty(ref _overlayAlignment, value);
    }

    public string OverlayPalette
    {
        get => _overlayPalette;
        set => SetProperty(ref _overlayPalette, value);
    }

    public int OverlayItemSpacing
    {
        get => _overlayItemSpacing;
        set => SetProperty(ref _overlayItemSpacing, value);
    }

    public int OverlayFontSize
    {
        get => _overlayFontSize;
        set => SetProperty(ref _overlayFontSize, value);
    }

    public bool OverlayBackgroundEnabled
    {
        get => _overlayBackgroundEnabled;
        set => SetProperty(ref _overlayBackgroundEnabled, value);
    }

    public string OverlayBackgroundColor
    {
        get => _overlayBackgroundColor;
        set => SetProperty(ref _overlayBackgroundColor, value);
    }

    public double OverlayBackgroundOpacity
    {
        get => _overlayBackgroundOpacity;
        set => SetProperty(ref _overlayBackgroundOpacity, value);
    }

    public void Load(UiSettings settings)
    {
        SampleIntervalMs = settings.SampleIntervalMs;
        Language = settings.Language;
        ThemeMode = settings.ThemeMode;
        CpuUsageDecimals = settings.CpuUsageDecimals;
        GpuUsageDecimals = settings.GpuUsageDecimals;
        MemoryUsageDecimals = settings.MemoryUsageDecimals;
        CpuTemperatureDecimals = settings.CpuTemperatureDecimals;
        GpuTemperatureDecimals = settings.GpuTemperatureDecimals;
        CpuPowerDecimals = settings.CpuPowerDecimals;
        GpuPowerDecimals = settings.GpuPowerDecimals;
        CpuClockUnit = settings.CpuClockUnit;
        CpuClockDecimals = settings.CpuClockDecimals;
        GpuClockUnit = settings.GpuClockUnit;
        GpuClockDecimals = settings.GpuClockDecimals;
        MemoryCapacityDecimals = settings.MemoryCapacityDecimals;
        NetworkSpeedUnit = settings.NetworkSpeedUnit;
        NetworkSpeedDecimals = settings.NetworkSpeedDecimals;
        TemperatureUnit = settings.TemperatureUnit;
        SelectedGpuId = settings.SelectedGpuId;
        SelectedNetworkAdapterId = settings.SelectedNetworkAdapterId;
        MetricGpuId = settings.MetricGpuId;
        MetricNetworkAdapterId = settings.MetricNetworkAdapterId;
        AutoStart = settings.AutoStart;
        CloseToTray = settings.CloseToTray;
        OverlayEnabled = settings.OverlayEnabled;
        OverlayEdge = settings.OverlayEdge;
        OverlayAlignment = settings.OverlayAlignment;
        OverlayPalette = settings.OverlayPalette;
        OverlayItemSpacing = settings.OverlayItemSpacing;
        OverlayFontSize = settings.OverlayFontSize;
        OverlayBackgroundEnabled = settings.OverlayBackgroundEnabled;
        OverlayBackgroundColor = settings.OverlayBackgroundColor;
        OverlayBackgroundOpacity = settings.OverlayBackgroundOpacity;
    }

    public void ApplyTo(UiSettings settings)
    {
        settings.SampleIntervalMs = SampleIntervalMs;
        settings.Language = Language;
        settings.ThemeMode = ThemeMode;
        settings.CpuUsageDecimals = CpuUsageDecimals;
        settings.GpuUsageDecimals = GpuUsageDecimals;
        settings.MemoryUsageDecimals = MemoryUsageDecimals;
        settings.CpuTemperatureDecimals = CpuTemperatureDecimals;
        settings.GpuTemperatureDecimals = GpuTemperatureDecimals;
        settings.CpuPowerDecimals = CpuPowerDecimals;
        settings.GpuPowerDecimals = GpuPowerDecimals;
        settings.CpuClockUnit = CpuClockUnit;
        settings.CpuClockDecimals = CpuClockDecimals;
        settings.GpuClockUnit = GpuClockUnit;
        settings.GpuClockDecimals = GpuClockDecimals;
        settings.MemoryCapacityDecimals = MemoryCapacityDecimals;
        settings.NetworkSpeedUnit = NetworkSpeedUnit;
        settings.NetworkSpeedDecimals = NetworkSpeedDecimals;
        settings.TemperatureUnit = TemperatureUnit;
        settings.SelectedGpuId = SelectedGpuId;
        settings.SelectedNetworkAdapterId = SelectedNetworkAdapterId;
        settings.MetricGpuId = MetricGpuId;
        settings.MetricNetworkAdapterId = MetricNetworkAdapterId;
        settings.AutoStart = AutoStart;
        settings.CloseToTray = CloseToTray;
        settings.OverlayEnabled = OverlayEnabled;
        settings.OverlayEdge = OverlayEdge;
        settings.OverlayAlignment = OverlayAlignment;
        settings.OverlayPalette = OverlayPalette;
        settings.OverlayItemSpacing = OverlayItemSpacing;
        settings.OverlayFontSize = OverlayFontSize;
        settings.OverlayBackgroundEnabled = OverlayBackgroundEnabled;
        settings.OverlayBackgroundColor = OverlayBackgroundColor;
        settings.OverlayBackgroundOpacity = OverlayBackgroundOpacity;
        settings.Normalize();
    }

    public UiSettings ToSettings()
    {
        var settings = new UiSettings();
        ApplyTo(settings);
        return settings;
    }
}
