namespace GearGauge.UI.ViewModels;

public sealed class SelectionOptionViewModel : ObservableViewModel
{
    private string _value = string.Empty;
    private string _label = string.Empty;

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }
}
