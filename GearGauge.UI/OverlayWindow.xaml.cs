using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using GearGauge.Core.Models;
using GearGauge.UI.Settings;
using GearGauge.UI.ViewModels;

namespace GearGauge.UI;

public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_NOACTIVATE = 0x08000000;

    private readonly ObservableCollection<OverlayMetricViewModel> _metrics = new();
    private string _currentEdge = OverlayEdge.Top;
    private int _currentSpacing = 6;

    public OverlayWindow()
    {
        InitializeComponent();
        DataContext = this;
        FontSize = 13;
        ContentRendered += OnContentRendered;
        UpdatePanelLayout();
    }

    public ObservableCollection<OverlayMetricViewModel> Metrics => _metrics;

    public void ApplyMetricsFromSettings(ObservableCollection<OverlayMetricViewModel> sourceMetrics)
    {
        for (var i = 0; i < sourceMetrics.Count; i++)
        {
            var src = sourceMetrics[i];
            if (i < _metrics.Count)
            {
                _metrics[i].MetricKey = src.MetricKey;
                _metrics[i].IsVisible = src.IsVisible;
                _metrics[i].Label = src.Label;
                _metrics[i].DisplayColor = src.DisplayColor;
                _metrics[i].ShowIcon = src.ShowIcon;
                _metrics[i].ShowLabel = src.ShowLabel;
                _metrics[i].IconGlyph = src.IconGlyph;
                _metrics[i].CustomLabel = src.CustomLabel;
            }
            else
            {
                _metrics.Add(new OverlayMetricViewModel
                {
                    MetricKey = src.MetricKey,
                    IsVisible = src.IsVisible,
                    Label = src.Label,
                    DisplayColor = src.DisplayColor,
                    ShowIcon = src.ShowIcon,
                    ShowLabel = src.ShowLabel,
                    IconGlyph = src.IconGlyph,
                    CustomLabel = src.CustomLabel
                });
            }
        }

        while (_metrics.Count > sourceMetrics.Count)
        {
            _metrics.RemoveAt(_metrics.Count - 1);
        }
    }

    public void ApplyStyle(UiSettings settings)
    {
        SetValue(FontSizeProperty, (double)Math.Max(8, Math.Min(36, settings.OverlayFontSize)));

        if (settings.OverlayBackgroundEnabled)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(settings.OverlayBackgroundColor);
                var opacity = Math.Min(1.0, Math.Max(0.0, settings.OverlayBackgroundOpacity));
                color.A = (byte)(opacity * 255);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                OverlayBackground.Background = brush;
            }
            catch
            {
                var fallback = Color.FromArgb(77, 0, 0, 0);
                var brush = new SolidColorBrush(fallback);
                brush.Freeze();
                OverlayBackground.Background = brush;
            }
        }
        else
        {
            OverlayBackground.Background = Brushes.Transparent;
        }
    }

    public void ApplySnapshot(HardwareMetrics snapshot, UiSettings settings, IReadOnlyDictionary<string, string> texts)
    {
        foreach (var metric in _metrics)
        {
            if (!metric.IsVisible) continue;
            metric.Value = ExtractMetricValue(metric.MetricKey, snapshot, settings, texts);
        }

        var edgeChanged = _currentEdge != settings.OverlayEdge;
        var spacingChanged = _currentSpacing != settings.OverlayItemSpacing;
        _currentEdge = settings.OverlayEdge;
        _currentSpacing = settings.OverlayItemSpacing;

        if (edgeChanged)
        {
            UpdatePanelLayout();
        }

        if (spacingChanged || edgeChanged)
        {
            UpdateItemSpacing();
        }

        UpdatePosition(settings.OverlayEdge, settings.OverlayAlignment);
    }

    public void SetLayout(string edge, string alignment, int spacing)
    {
        var edgeChanged = _currentEdge != edge;
        var spacingChanged = _currentSpacing != spacing;
        _currentEdge = edge;
        _currentSpacing = spacing;

        if (edgeChanged) UpdatePanelLayout();
        if (spacingChanged || edgeChanged) UpdateItemSpacing();
        UpdatePosition(edge, alignment);
    }

    public void UpdatePosition(string edge, string alignment)
    {
        var workArea = SystemParameters.WorkArea;
        const double margin = 8;

        UpdateLayout();
        var w = ActualWidth > 0 ? ActualWidth : 200;
        var h = ActualHeight > 0 ? ActualHeight : 200;

        double left, top;

        if (string.Equals(edge, OverlayEdge.Bottom, StringComparison.OrdinalIgnoreCase))
        {
            top = workArea.Bottom - h - margin;
            left = alignment switch
            {
                "Center" => workArea.Left + (workArea.Width - w) / 2,
                "End" => workArea.Right - w - margin,
                _ => workArea.Left + margin
            };
        }
        else if (string.Equals(edge, OverlayEdge.Left, StringComparison.OrdinalIgnoreCase))
        {
            left = workArea.Left + margin;
            top = alignment switch
            {
                "Center" => workArea.Top + (workArea.Height - h) / 2,
                "End" => workArea.Bottom - h - margin,
                _ => workArea.Top + margin
            };
        }
        else if (string.Equals(edge, OverlayEdge.Right, StringComparison.OrdinalIgnoreCase))
        {
            left = workArea.Right - w - margin;
            top = alignment switch
            {
                "Center" => workArea.Top + (workArea.Height - h) / 2,
                "End" => workArea.Bottom - h - margin,
                _ => workArea.Top + margin
            };
        }
        else // Top (default)
        {
            top = workArea.Top + margin;
            left = alignment switch
            {
                "Center" => workArea.Left + (workArea.Width - w) / 2,
                "End" => workArea.Right - w - margin,
                _ => workArea.Left + margin
            };
        }

        Left = left;
        Top = top;
    }

    private void UpdatePanelLayout()
    {
        var isHorizontal = string.Equals(_currentEdge, OverlayEdge.Top, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(_currentEdge, OverlayEdge.Bottom, StringComparison.OrdinalIgnoreCase);

        if (isHorizontal)
        {
            var factory = new FrameworkElementFactory(typeof(WrapPanel));
            factory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            MetricsItems.ItemsPanel = new ItemsPanelTemplate(factory);
        }
        else
        {
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            MetricsItems.ItemsPanel = new ItemsPanelTemplate(factory);
        }
    }

    private void UpdateItemSpacing()
    {
        var s = _currentSpacing;
        var isHorizontal = string.Equals(_currentEdge, OverlayEdge.Top, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(_currentEdge, OverlayEdge.Bottom, StringComparison.OrdinalIgnoreCase);

        Thickness margin;
        if (isHorizontal)
        {
            margin = new Thickness(s, 2, s, 2);
        }
        else
        {
            margin = new Thickness(6, s, 6, s);
        }

        var style = new Style(typeof(ContentPresenter));
        style.Setters.Add(new Setter(MarginProperty, margin));
        MetricsItems.ItemContainerStyle = style;
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        MakeClickThrough();
    }

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, (int)(extendedStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE));
    }

    private static string ExtractMetricValue(string metricKey, HardwareMetrics snapshot, UiSettings settings, IReadOnlyDictionary<string, string> texts)
    {
        return metricKey switch
        {
            OverlayMetricKeys.CpuUsage => UiValueFormatter.FormatPercent(snapshot.Cpu.UsagePercent, settings.CpuUsageDecimals),
            OverlayMetricKeys.CpuTemp => UiValueFormatter.FormatTemperature(snapshot.Cpu.TemperatureCelsius, settings, settings.CpuTemperatureDecimals),
            OverlayMetricKeys.CpuPower => UiValueFormatter.FormatPower(snapshot.Cpu.PowerWatt, settings.CpuPowerDecimals),
            OverlayMetricKeys.CpuClock => UiValueFormatter.FormatCpuClock(snapshot.Cpu.ClockGHz, settings),
            OverlayMetricKeys.GpuUsage => snapshot.Gpus.Count > 0
                ? UiValueFormatter.FormatPercent(snapshot.Gpus[0].UsagePercent, settings.GpuUsageDecimals)
                : "N/A",
            OverlayMetricKeys.GpuTemp => snapshot.Gpus.Count > 0
                ? UiValueFormatter.FormatTemperature(snapshot.Gpus[0].TemperatureCelsius, settings, settings.GpuTemperatureDecimals)
                : "N/A",
            OverlayMetricKeys.GpuPower => snapshot.Gpus.Count > 0
                ? UiValueFormatter.FormatPower(snapshot.Gpus[0].PowerWatt, settings.GpuPowerDecimals)
                : "N/A",
            OverlayMetricKeys.GpuClock => snapshot.Gpus.Count > 0
                ? UiValueFormatter.FormatGpuClock(snapshot.Gpus[0].ClockMHz, settings)
                : "N/A",
            OverlayMetricKeys.MemUsed => UiValueFormatter.FormatMemory(snapshot.Memory.UsedGB, settings.MemoryCapacityDecimals),
            OverlayMetricKeys.MemUsage => UiValueFormatter.FormatPercent(snapshot.Memory.UsagePercent, settings.MemoryUsageDecimals),
            OverlayMetricKeys.FpsDisplay => UiValueFormatter.FormatOptional(snapshot.Fps.DisplayOutputFps, "FPS", 2),
            OverlayMetricKeys.FpsGame => UiValueFormatter.FormatOptional(snapshot.Fps.GameFps, "FPS", 2),
            OverlayMetricKeys.NetDownload => snapshot.NetworkAdapters.Count > 0
                ? UiValueFormatter.FormatNetworkSpeed(snapshot.NetworkAdapters[0].DownloadMbps, settings)
                : "N/A",
            OverlayMetricKeys.NetUpload => snapshot.NetworkAdapters.Count > 0
                ? UiValueFormatter.FormatNetworkSpeed(snapshot.NetworkAdapters[0].UploadMbps, settings)
                : "N/A",
            _ => "N/A"
        };
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
