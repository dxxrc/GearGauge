namespace GearGauge.UI.ViewModels;

public sealed class CpuCoreViewModel : ObservableViewModel
{
    private int _index;
    private string _title = string.Empty;
    private string _usageText = string.Empty;
    private string _clockText = string.Empty;
    private string _temperatureText = string.Empty;
    private string _powerText = string.Empty;

    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string UsageText
    {
        get => _usageText;
        set => SetProperty(ref _usageText, value);
    }

    public string ClockText
    {
        get => _clockText;
        set => SetProperty(ref _clockText, value);
    }

    public string TemperatureText
    {
        get => _temperatureText;
        set => SetProperty(ref _temperatureText, value);
    }

    public string PowerText
    {
        get => _powerText;
        set => SetProperty(ref _powerText, value);
    }
}
