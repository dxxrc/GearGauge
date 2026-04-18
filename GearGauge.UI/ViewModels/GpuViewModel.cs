namespace GearGauge.UI.ViewModels;

public sealed class GpuViewModel : ObservableViewModel
{
    private string _id = string.Empty;
    private string _header = string.Empty;
    private string _usageText = string.Empty;
    private string _temperatureText = string.Empty;
    private string _powerText = string.Empty;
    private string _clockText = string.Empty;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    public string UsageText
    {
        get => _usageText;
        set => SetProperty(ref _usageText, value);
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

    public string ClockText
    {
        get => _clockText;
        set => SetProperty(ref _clockText, value);
    }
}
