using System.Collections.ObjectModel;
using GearGauge.UI.Settings;

namespace GearGauge.UI.ViewModels;

public sealed class TaskbarSettingsViewModel : ObservableViewModel
{
    private bool _taskbarWidgetEnabled;
    private string _selectedPalette = OverlayPaletteNames.NeonCyber;
    private int _taskbarFontSize = 11;
    private int _taskbarRowSpacing = 2;
    private int _taskbarMaxRows = 2;
    private string _taskbarPosition = TaskbarPositionNames.LeftOfTray;

    public event Action? Changed;

    public bool TaskbarWidgetEnabled
    {
        get => _taskbarWidgetEnabled;
        set { if (SetProperty(ref _taskbarWidgetEnabled, value)) Changed?.Invoke(); }
    }

    public string SelectedPalette
    {
        get => _selectedPalette;
        set
        {
            if (SetProperty(ref _selectedPalette, value))
            {
                ApplyPalette(value);
                Changed?.Invoke();
            }
        }
    }

    public int TaskbarFontSize
    {
        get => _taskbarFontSize;
        set { if (SetProperty(ref _taskbarFontSize, value)) Changed?.Invoke(); }
    }

    public int TaskbarRowSpacing
    {
        get => _taskbarRowSpacing;
        set { if (SetProperty(ref _taskbarRowSpacing, value)) Changed?.Invoke(); }
    }

    public int TaskbarMaxRows
    {
        get => _taskbarMaxRows;
        set { if (SetProperty(ref _taskbarMaxRows, value)) Changed?.Invoke(); }
    }

    public string TaskbarPosition
    {
        get => _taskbarPosition;
        set { if (SetProperty(ref _taskbarPosition, value)) Changed?.Invoke(); }
    }

    public ObservableCollection<OverlayMetricViewModel> Metrics { get; } = new();
    public ObservableCollection<SelectionOptionViewModel> PaletteOptions { get; } = new();
    public ObservableCollection<SelectionOptionViewModel> PositionOptions { get; } = new();

    public void Load(UiSettings settings, IReadOnlyDictionary<string, string> texts)
    {
        UnsubscribeMetricEvents();

        TaskbarWidgetEnabled = settings.TaskbarWidgetEnabled;
        SelectedPalette = settings.TaskbarPalette;
        TaskbarFontSize = settings.TaskbarFontSize;
        TaskbarRowSpacing = settings.TaskbarRowSpacing;
        TaskbarMaxRows = settings.TaskbarMaxRows;
        TaskbarPosition = settings.TaskbarPosition;

        SyncPaletteOptions(texts);
        SyncPositionOptions(texts);
        SyncMetrics(settings.TaskbarMetrics, texts);

        foreach (var metric in Metrics)
            metric.PropertyChanged += OnMetricPropertyChanged;
    }

    public void ApplyTo(UiSettings settings)
    {
        settings.TaskbarWidgetEnabled = TaskbarWidgetEnabled;
        settings.TaskbarPalette = SelectedPalette;
        settings.TaskbarFontSize = TaskbarFontSize;
        settings.TaskbarRowSpacing = TaskbarRowSpacing;
        settings.TaskbarMaxRows = TaskbarMaxRows;
        settings.TaskbarPosition = TaskbarPosition;

        var configs = new List<OverlayMetricConfig>();
        foreach (var metric in Metrics)
        {
            configs.Add(new OverlayMetricConfig
            {
                MetricKey = metric.MetricKey,
                IsVisible = metric.IsVisible,
                ShowIcon = metric.ShowIcon,
                ShowLabel = metric.ShowLabel,
                DisplayColor = metric.DisplayColor,
                CustomLabel = metric.CustomLabel
            });
        }
        settings.TaskbarMetrics = configs;
    }

    public void ApplyPalette(string paletteName)
    {
        var palette = SciFiPalettes.Get(paletteName);
        foreach (var metric in Metrics)
        {
            if (palette.TryGetValue(metric.MetricKey, out var color))
                metric.DisplayColor = color;
        }
    }

    public void ApplyUniformColor(string hexColor)
    {
        foreach (var metric in Metrics)
        {
            if (string.Equals(metric.DisplayColor, hexColor, StringComparison.OrdinalIgnoreCase))
                metric.DisplayColor = string.Empty;
            metric.DisplayColor = hexColor;
        }
        Changed?.Invoke();
    }

    public void MoveMetricUp(int index)
    {
        if (index <= 0 || index >= Metrics.Count) return;
        Metrics.Move(index, index - 1);
        Changed?.Invoke();
    }

    public void MoveMetricDown(int index)
    {
        if (index < 0 || index >= Metrics.Count - 1) return;
        Metrics.Move(index, index + 1);
        Changed?.Invoke();
    }

    private void OnMetricPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => Changed?.Invoke();

    private void UnsubscribeMetricEvents()
    {
        foreach (var metric in Metrics)
        {
            metric.PropertyChanged -= OnMetricPropertyChanged;
        }
    }

    private void SyncPaletteOptions(IReadOnlyDictionary<string, string> texts)
    {
        SyncOptions(PaletteOptions,
        [
            new SelectionOptionViewModel { Value = OverlayPaletteNames.NeonCyber, Label = GetText(texts, "PaletteNeonCyber") },
            new SelectionOptionViewModel { Value = OverlayPaletteNames.QuantumIce, Label = GetText(texts, "PaletteQuantumIce") },
            new SelectionOptionViewModel { Value = OverlayPaletteNames.SolarFire, Label = GetText(texts, "PaletteSolarFire") },
            new SelectionOptionViewModel { Value = OverlayPaletteNames.MatrixGreen, Label = GetText(texts, "PaletteMatrixGreen") },
            new SelectionOptionViewModel { Value = OverlayPaletteNames.PlasmaViolet, Label = GetText(texts, "PalettePlasmaViolet") },
            new SelectionOptionViewModel { Value = OverlayPaletteNames.Titanium, Label = GetText(texts, "PaletteTitanium") },
            new SelectionOptionViewModel { Value = OverlayPaletteNames.EmeraldCircuit, Label = GetText(texts, "PaletteEmeraldCircuit") },
            new SelectionOptionViewModel { Value = OverlayPaletteNames.PhantomRed, Label = GetText(texts, "PalettePhantomRed") }
        ]);
    }

    private void SyncPositionOptions(IReadOnlyDictionary<string, string> texts)
    {
        SyncOptions(PositionOptions,
        [
            new SelectionOptionViewModel { Value = TaskbarPositionNames.LeftOfTray, Label = GetText(texts, "TaskbarLeftOfTray") },
            new SelectionOptionViewModel { Value = TaskbarPositionNames.RightOfStart, Label = GetText(texts, "TaskbarRightOfStart") },
            new SelectionOptionViewModel { Value = TaskbarPositionNames.Leftmost, Label = GetText(texts, "TaskbarLeftmost") }
        ]);
    }

    private void SyncMetrics(List<OverlayMetricConfig> configs, IReadOnlyDictionary<string, string> texts)
    {
        var iconGlyphs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [OverlayMetricKeys.CpuUsage] = "\uEEA1",
            [OverlayMetricKeys.CpuTemp] = "\uE806",
            [OverlayMetricKeys.CpuPower] = "\uE83F",
            [OverlayMetricKeys.CpuClock] = "\uEC92",
            [OverlayMetricKeys.GpuUsage] = "\uE967",
            [OverlayMetricKeys.GpuTemp] = "\uE806",
            [OverlayMetricKeys.GpuPower] = "\uE83F",
            [OverlayMetricKeys.GpuClock] = "\uEC92",
            [OverlayMetricKeys.MemUsed] = "\uEEA0",
            [OverlayMetricKeys.MemUsage] = "\uEEA0",
            [OverlayMetricKeys.FpsDisplay] = "\uEC4A",
            [OverlayMetricKeys.FpsGame] = "\uEC4A",
            [OverlayMetricKeys.NetDownload] = "\uEE77",
            [OverlayMetricKeys.NetUpload] = "\uEE77"
        };

        for (var i = 0; i < configs.Count; i++)
        {
            var config = configs[i];
            if (i < Metrics.Count)
            {
                Metrics[i].MetricKey = config.MetricKey;
                Metrics[i].IsVisible = config.IsVisible;
                Metrics[i].ShowIcon = config.ShowIcon;
                Metrics[i].ShowLabel = config.ShowLabel;
                Metrics[i].DisplayColor = config.DisplayColor;
                Metrics[i].CustomLabel = config.CustomLabel;
                Metrics[i].Label = string.IsNullOrWhiteSpace(config.CustomLabel)
                        ? GetText(texts, $"Overlay_{config.MetricKey}")
                        : config.CustomLabel;
                Metrics[i].IconGlyph = iconGlyphs.TryGetValue(config.MetricKey, out var glyph) ? glyph : string.Empty;
            }
            else
            {
                Metrics.Add(new OverlayMetricViewModel
                {
                    MetricKey = config.MetricKey,
                    IsVisible = config.IsVisible,
                    ShowIcon = config.ShowIcon,
                    ShowLabel = config.ShowLabel,
                    DisplayColor = config.DisplayColor,
                    CustomLabel = config.CustomLabel,
                    Label = string.IsNullOrWhiteSpace(config.CustomLabel)
                        ? GetText(texts, $"Overlay_{config.MetricKey}")
                        : config.CustomLabel,
                    IconGlyph = iconGlyphs.TryGetValue(config.MetricKey, out var glyph) ? glyph : string.Empty
                });
            }
        }

        while (Metrics.Count > configs.Count)
            Metrics.RemoveAt(Metrics.Count - 1);
    }

    private static void SyncOptions(ObservableCollection<SelectionOptionViewModel> target, SelectionOptionViewModel[] options)
    {
        for (var i = 0; i < options.Length; i++)
        {
            if (i < target.Count)
            {
                target[i].Value = options[i].Value;
                target[i].Label = options[i].Label;
            }
            else
            {
                target.Add(options[i]);
            }
        }

        while (target.Count > options.Length)
            target.RemoveAt(target.Count - 1);
    }

    private static string GetText(IReadOnlyDictionary<string, string> texts, string key) =>
        texts.TryGetValue(key, out var value) ? value : key;
}
