using System.Collections.ObjectModel;

namespace GearGauge.UI.ViewModels;

public sealed class MainWindowViewModel : ObservableViewModel
{
    private string _statusText = "Initializing...";
    private string _lastUpdatedText = "No samples yet";
    private string _toastText = string.Empty;
    private bool _isToastVisible;
    private string _cpuHeader = "CPU";
    private string _dataSourceBadge = string.Empty;
    private string _elevationBadge = string.Empty;
    private bool _canElevate;
    private string _selectedPage = "monitor";
    private IReadOnlyDictionary<string, string> _texts = new Dictionary<string, string>();

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        set => SetProperty(ref _lastUpdatedText, value);
    }

    public string ToastText
    {
        get => _toastText;
        set => SetProperty(ref _toastText, value);
    }

    public bool IsToastVisible
    {
        get => _isToastVisible;
        set => SetProperty(ref _isToastVisible, value);
    }

    public string CpuHeader
    {
        get => _cpuHeader;
        set => SetProperty(ref _cpuHeader, value);
    }

    public string DataSourceBadge
    {
        get => _dataSourceBadge;
        set => SetProperty(ref _dataSourceBadge, value);
    }

    public string ElevationBadge
    {
        get => _elevationBadge;
        set => SetProperty(ref _elevationBadge, value);
    }

    public bool CanElevate
    {
        get => _canElevate;
        set => SetProperty(ref _canElevate, value);
    }

    public string SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (SetProperty(ref _selectedPage, value))
            {
                RaisePropertyChanged(nameof(IsMonitorPageSelected));
                RaisePropertyChanged(nameof(IsSettingsPageSelected));
                RaisePropertyChanged(nameof(IsOverlayPageSelected));
                RaisePropertyChanged(nameof(IsTaskbarPageSelected));
            }
        }
    }

    public bool IsMonitorPageSelected
    {
        get => SelectedPage == "monitor";
        set
        {
            if (value)
            {
                SelectedPage = "monitor";
                RaisePropertyChanged(nameof(IsSettingsPageSelected));
                RaisePropertyChanged(nameof(IsOverlayPageSelected));
                RaisePropertyChanged(nameof(IsTaskbarPageSelected));
            }
        }
    }

    public bool IsSettingsPageSelected
    {
        get => SelectedPage == "settings";
        set
        {
            if (value)
            {
                SelectedPage = "settings";
                RaisePropertyChanged(nameof(IsMonitorPageSelected));
                RaisePropertyChanged(nameof(IsOverlayPageSelected));
                RaisePropertyChanged(nameof(IsTaskbarPageSelected));
            }
        }
    }

    public bool IsOverlayPageSelected
    {
        get => SelectedPage == "overlay";
        set
        {
            if (value)
            {
                SelectedPage = "overlay";
                RaisePropertyChanged(nameof(IsMonitorPageSelected));
                RaisePropertyChanged(nameof(IsSettingsPageSelected));
                RaisePropertyChanged(nameof(IsTaskbarPageSelected));
            }
        }
    }

    public bool IsTaskbarPageSelected
    {
        get => SelectedPage == "taskbar";
        set
        {
            if (value)
            {
                SelectedPage = "taskbar";
                RaisePropertyChanged(nameof(IsMonitorPageSelected));
                RaisePropertyChanged(nameof(IsSettingsPageSelected));
                RaisePropertyChanged(nameof(IsOverlayPageSelected));
            }
        }
    }

    public IReadOnlyDictionary<string, string> Texts
    {
        get => _texts;
        set => SetProperty(ref _texts, value);
    }

    public ThemeViewModel Theme { get; } = new();

    public UiSettingsViewModel Settings { get; } = new();

    public OverlaySettingsViewModel OverlaySettings { get; } = new();

    public TaskbarSettingsViewModel TaskbarSettings { get; } = new();

    public ObservableCollection<SelectionOptionViewModel> LanguageOptions { get; } = new();

    public ObservableCollection<SelectionOptionViewModel> ThemeOptions { get; } = new();

    public ObservableCollection<SelectionOptionViewModel> TemperatureUnitOptions { get; } = new();

    public ObservableCollection<SelectionOptionViewModel> GpuOptions { get; } = new();

    public ObservableCollection<SelectionOptionViewModel> NetworkAdapterOptions { get; } = new();

    public ObservableCollection<SelectionOptionViewModel> MetricGpuOptions { get; } = new();

    public ObservableCollection<SelectionOptionViewModel> MetricNetworkAdapterOptions { get; } = new();

    public ObservableCollection<MetricRowViewModel> CpuSummary { get; } = new();

    public ObservableCollection<CpuCoreViewModel> CpuCores { get; } = new();

    public ObservableCollection<GpuViewModel> Gpus { get; } = new();

    public ObservableCollection<MetricRowViewModel> MemoryRows { get; } = new();

    public ObservableCollection<MetricRowViewModel> FpsRows { get; } = new();

    public ObservableCollection<NetworkAdapterViewModel> NetworkAdapters { get; } = new();
}
