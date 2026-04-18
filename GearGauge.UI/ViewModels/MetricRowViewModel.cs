namespace GearGauge.UI.ViewModels;

public sealed class MetricRowViewModel : ObservableViewModel
{
    private string _label = string.Empty;
    private string _value = string.Empty;

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
}
