using System.Collections.ObjectModel;
using GearGauge.UI.Settings;

namespace GearGauge.UI.ViewModels;

public sealed class OverlaySettingsViewModel : ObservableViewModel
{
    private bool _overlayEnabled;
    private string _selectedEdge = OverlayEdge.Top;
    private string _selectedAlignment = OverlayAlignment.Start;
    private string _selectedPalette = OverlayPaletteNames.NeonCyber;
    private int _overlayItemSpacing = 6;
    private int _overlayFontSize = 13;
    private bool _overlayBackgroundEnabled = true;
    private string _overlayBackgroundColor = "#000000";
    private double _overlayBackgroundOpacity = 0.3;

    public event Action? Changed;

    public bool OverlayEnabled
    {
        get => _overlayEnabled;
        set
        {
            if (SetProperty(ref _overlayEnabled, value))
                Changed?.Invoke();
        }
    }

    public string SelectedEdge
    {
        get => _selectedEdge;
        set
        {
            if (SetProperty(ref _selectedEdge, value))
                Changed?.Invoke();
        }
    }

    public string SelectedAlignment
    {
        get => _selectedAlignment;
        set
        {
            if (SetProperty(ref _selectedAlignment, value))
                Changed?.Invoke();
        }
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

    public int OverlayItemSpacing
    {
        get => _overlayItemSpacing;
        set
        {
            if (SetProperty(ref _overlayItemSpacing, value))
                Changed?.Invoke();
        }
    }

    public int OverlayFontSize
    {
        get => _overlayFontSize;
        set
        {
            if (SetProperty(ref _overlayFontSize, value))
                Changed?.Invoke();
        }
    }

    public bool OverlayBackgroundEnabled
    {
        get => _overlayBackgroundEnabled;
        set
        {
            if (SetProperty(ref _overlayBackgroundEnabled, value))
                Changed?.Invoke();
        }
    }

    public string OverlayBackgroundColor
    {
        get => _overlayBackgroundColor;
        set
        {
            if (SetProperty(ref _overlayBackgroundColor, value))
                Changed?.Invoke();
        }
    }

    public double OverlayBackgroundOpacity
    {
        get => _overlayBackgroundOpacity;
        set
        {
            if (SetProperty(ref _overlayBackgroundOpacity, value))
                Changed?.Invoke();
        }
    }

    public ObservableCollection<OverlayMetricViewModel> Metrics { get; } = new();
    public ObservableCollection<SelectionOptionViewModel> EdgeOptions { get; } = new();
    public ObservableCollection<SelectionOptionViewModel> AlignmentOptions { get; } = new();
    public ObservableCollection<SelectionOptionViewModel> PaletteOptions { get; } = new();

    public void Load(UiSettings settings, IReadOnlyDictionary<string, string> texts)
    {
        UnsubscribeMetricEvents();

        OverlayEnabled = settings.OverlayEnabled;
        SelectedEdge = settings.OverlayEdge;
        SelectedAlignment = settings.OverlayAlignment;
        SelectedPalette = settings.OverlayPalette;
        OverlayItemSpacing = settings.OverlayItemSpacing;
        OverlayFontSize = settings.OverlayFontSize;
        OverlayBackgroundEnabled = settings.OverlayBackgroundEnabled;
        OverlayBackgroundColor = settings.OverlayBackgroundColor;
        OverlayBackgroundOpacity = settings.OverlayBackgroundOpacity;

        SyncEdgeOptions(texts);
        SyncAlignmentOptions(texts);
        SyncPaletteOptions(texts);
        SyncMetrics(settings.OverlayMetrics, texts);

        foreach (var metric in Metrics)
        {
            metric.PropertyChanged += OnMetricPropertyChanged;
        }
    }

    public void ApplyTo(UiSettings settings)
    {
        settings.OverlayEnabled = OverlayEnabled;
        settings.OverlayEdge = SelectedEdge;
        settings.OverlayAlignment = SelectedAlignment;
        settings.OverlayPalette = SelectedPalette;
        settings.OverlayItemSpacing = OverlayItemSpacing;
        settings.OverlayFontSize = OverlayFontSize;
        settings.OverlayBackgroundEnabled = OverlayBackgroundEnabled;
        settings.OverlayBackgroundColor = OverlayBackgroundColor;
        settings.OverlayBackgroundOpacity = OverlayBackgroundOpacity;

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

        settings.OverlayMetrics = configs;
    }

    public void ApplyPalette(string paletteName)
    {
        var palette = SciFiPalettes.Get(paletteName);
        foreach (var metric in Metrics)
        {
            if (palette.TryGetValue(metric.MetricKey, out var color))
            {
                metric.DisplayColor = color;
            }
        }
    }

    public void ApplyUniformColor(string hexColor)
    {
        foreach (var metric in Metrics)
        {
            // 如果当前颜色已经和目标相同，SetProperty 会跳过不通知 UI，
            // 先清空再赋值，强制触发 PropertyChanged。
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
    {
        Changed?.Invoke();
    }

    private void UnsubscribeMetricEvents()
    {
        foreach (var metric in Metrics)
        {
            metric.PropertyChanged -= OnMetricPropertyChanged;
        }
    }

    private void SyncEdgeOptions(IReadOnlyDictionary<string, string> texts)
    {
        SyncOptions(EdgeOptions,
        [
            new SelectionOptionViewModel { Value = OverlayEdge.Top, Label = GetText(texts, "EdgeTop") },
            new SelectionOptionViewModel { Value = OverlayEdge.Bottom, Label = GetText(texts, "EdgeBottom") },
            new SelectionOptionViewModel { Value = OverlayEdge.Left, Label = GetText(texts, "EdgeLeft") },
            new SelectionOptionViewModel { Value = OverlayEdge.Right, Label = GetText(texts, "EdgeRight") }
        ]);
    }

    private void SyncAlignmentOptions(IReadOnlyDictionary<string, string> texts)
    {
        SyncOptions(AlignmentOptions,
        [
            new SelectionOptionViewModel { Value = OverlayAlignment.Start, Label = GetText(texts, "AlignStart") },
            new SelectionOptionViewModel { Value = OverlayAlignment.Center, Label = GetText(texts, "AlignCenter") },
            new SelectionOptionViewModel { Value = OverlayAlignment.End, Label = GetText(texts, "AlignEnd") }
        ]);
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
        {
            Metrics.RemoveAt(Metrics.Count - 1);
        }
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
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static string GetText(IReadOnlyDictionary<string, string> texts, string key) =>
        texts.TryGetValue(key, out var value) ? value : key;
}
