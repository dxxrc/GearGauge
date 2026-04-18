namespace GearGauge.UI.ViewModels;

public sealed class OverlayMetricViewModel : ObservableViewModel
{
    private bool _isVisible = true;
    private string _metricKey = string.Empty;
    private string _label = string.Empty;
    private string _value = string.Empty;
    private string _displayColor = "#00FFCC";
    private bool _showIcon = true;
    private bool _showLabel = true;
    private string _iconGlyph = string.Empty;
    private string _customLabel = string.Empty;

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public string MetricKey
    {
        get => _metricKey;
        set => SetProperty(ref _metricKey, value);
    }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public string DisplayColor
    {
        get => _displayColor;
        set => SetProperty(ref _displayColor, value);
    }

    public bool ShowIcon
    {
        get => _showIcon;
        set => SetProperty(ref _showIcon, value);
    }

    public bool ShowLabel
    {
        get => _showLabel;
        set => SetProperty(ref _showLabel, value);
    }

    public string IconGlyph
    {
        get => _iconGlyph;
        set => SetProperty(ref _iconGlyph, value);
    }

    public string CustomLabel
    {
        get => _customLabel;
        set => SetProperty(ref _customLabel, value);
    }
}
