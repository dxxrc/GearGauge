namespace GearGauge.UI.ViewModels;

public sealed class NetworkAdapterViewModel : ObservableViewModel
{
    private string _adapterId = string.Empty;
    private string _header = string.Empty;
    private string _stateText = string.Empty;
    private string _downloadText = string.Empty;
    private string _uploadText = string.Empty;

    public string AdapterId
    {
        get => _adapterId;
        set => SetProperty(ref _adapterId, value);
    }

    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    public string StateText
    {
        get => _stateText;
        set => SetProperty(ref _stateText, value);
    }

    public string DownloadText
    {
        get => _downloadText;
        set => SetProperty(ref _downloadText, value);
    }

    public string UploadText
    {
        get => _uploadText;
        set => SetProperty(ref _uploadText, value);
    }
}
