using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GearGauge.Core.Models;
using GearGauge.UI.Settings;
using GearGauge.UI.ViewModels;

namespace GearGauge.UI.Taskbar;

/// <summary>
/// 任务栏小组件窗口。完全透明背景，只显示文字图标，不遮挡任务栏。
/// 使用 WPF Window + AllowsTransparency 实现真正的逐像素透明。
/// Win10: SetParent 嵌入 ReBarWindow32。
/// Win11: Topmost 悬浮窗口定位在任务栏屏幕坐标处。
/// 布局：列优先，指标按列纵向排列，每列最多 maxRows 行，列自适应宽度。
/// </summary>
internal sealed class TaskbarWidgetWindow : IDisposable
{
    private Window? _window;
    private IntPtr _hwnd;
    private readonly TaskbarPlacementService _placementService = new();
    private readonly TaskbarPlatformDetector _platformDetector = new();
    private readonly DispatcherTimer _embedRetryTimer;
    private System.Threading.Timer? _zorderTimer;
    private readonly ObservableCollection<OverlayMetricViewModel> _metrics = new();
    private uint _taskbarCreatedMsg;
    private bool _isEmbedded;
    private bool _isDisposed;
    private int _embedRetryCount;
    private int _overlayHealthTick;
    private const int MaxEmbedRetries = 12;

    private bool _isOverlayMode;
    private int _currentMaxRows = 2;
    private int _currentRowSpacing = 2;
    private string _currentPosition = TaskbarPositionNames.LeftOfTray;
    private TaskbarNativeInterop.RECT _lastStartButtonRect;
    private int _stableStartButtonSamples;
    private const int StartButtonStableSampleTarget = 2;

    // WPF 控件引用
    private Grid? _rootGrid;
    private int _currentFontSize = 11;
    private readonly List<StackPanel> _columnPanels = new();
    private readonly List<TextBlock> _labelTextBlocks = new();

    public IntPtr Handle => _hwnd;
    public bool IsEmbedded => _isEmbedded;

    public TaskbarWidgetWindow()
    {
        _embedRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _embedRetryTimer.Tick += OnEmbedRetry;

        _taskbarCreatedMsg = _placementService.GetTaskbarCreatedMessage();
    }

    public void Create()
    {
        _rootGrid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        _rootGrid.SetValue(TextElement.FontSizeProperty, (double)_currentFontSize);
        RebuildLayout();

        var visibleCount = _metrics.Count(m => m.IsVisible);
        var maxPhysical = GetAvailableWidthPhysical();
        var initialMaxDip = PhysicalToDip(maxPhysical);
        var initialWidth = CalculateWidgetWidthDip(visibleCount, initialMaxDip);
        var (_, tbHeight) = _platformDetector.GetTaskbarSize();
        var initialHeight = PhysicalToDip(tbHeight > 0 ? tbHeight : 48);

        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            ShowInTaskbar = false,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            Focusable = false,
            Opacity = 0,
            Width = initialWidth,
            Height = initialHeight,
            Left = -32000,
            Top = -32000,
            Content = _rootGrid,
            ShowActivated = false,
        };

        // 不设置 Window.Topmost — 完全通过 Win32 SetWindowPos 管理 z-order，
        // 避免 WPF 内部的 Topmost 管理与 Win32 调用冲突。

        _window.SourceInitialized += OnSourceInitialized;
        _window.Show();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(_window!);
        _hwnd = helper.Handle;

        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        TaskbarNativeInterop.ShowWindow(_hwnd, TaskbarNativeInterop.SW_HIDE);
    }

    /// <summary>
    /// 将物理像素值转换为 WPF DIP 值。
    /// </summary>
    private double PhysicalToDip(int physical)
    {
        if (_hwnd == IntPtr.Zero) return physical;
        var source = HwndSource.FromHwnd(_hwnd);
        if (source?.CompositionTarget == null) return physical;
        return physical * source.CompositionTarget.TransformFromDevice.M11;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_taskbarCreatedMsg != 0 && msg == (int)_taskbarCreatedMsg)
        {
            if (!_isOverlayMode)
            {
                _placementService.DetachWidget();
            }

            _isEmbedded = false;
            _embedRetryCount = 0;
            ResetStartButtonStability();
            ScheduleEmbedRetry();
            TryEmbed();
            handled = true;
        }

        if (msg == TaskbarNativeInterop.WM_DPICHANGED || msg == TaskbarNativeInterop.WM_DISPLAYCHANGE)
        {
            Reposition();
        }

        // 拦截 z-order 变更，强制保持 HWND_TOPMOST
        if (msg == TaskbarNativeInterop.WM_WINDOWPOSCHANGING && _isOverlayMode && !_isDisposed)
        {
            var pos = Marshal.PtrToStructure<TaskbarNativeInterop.WINDOWPOS>(lParam);
            if ((pos.flags & TaskbarNativeInterop.SWP_NOZORDER) == 0)
            {
                pos.hwndInsertAfter = TaskbarNativeInterop.HWND_TOPMOST;
                Marshal.StructureToPtr(pos, lParam, false);
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 重建列优先布局。
    /// 第1个指标 → 第1列第1行，第2个 → 第1列第2行，第3个 → 第2列第1行 …
    /// 每列按内容自适应宽度，居左对齐。
    /// </summary>
    private void RebuildLayout()
    {
        if (_rootGrid == null) return;

        _rootGrid.ColumnDefinitions.Clear();
        _rootGrid.Children.Clear();
        _columnPanels.Clear();
        _labelTextBlocks.Clear();

        var visibleMetrics = _metrics.Where(m => m.IsVisible).ToList();
        if (visibleMetrics.Count == 0) return;

        var maxRows = Math.Max(1, _currentMaxRows);
        var columnCount = (int)Math.Ceiling((double)visibleMetrics.Count / maxRows);

        Grid.SetIsSharedSizeScope(_rootGrid, false);

        for (var col = 0; col < columnCount; col++)
        {
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        }

        for (var col = 0; col < columnCount; col++)
        {
            var column = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(6, 0, 6, 0)
            };
            _columnPanels.Add(column);

            for (var row = 0; row < maxRows; row++)
            {
                var idx = col * maxRows + row;
                if (idx >= visibleMetrics.Count) break;

                column.Children.Add(CreateMetricCell(visibleMetrics[idx]));
            }

            Grid.SetColumn(column, col);
            _rootGrid.Children.Add(column);
        }
    }

    private FrameworkElement CreateMetricCell(OverlayMetricViewModel metric)
    {
        var sp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, _currentRowSpacing, 0, _currentRowSpacing)
        };

        var converter = new Converters.StringToBrushConverter();

        if (metric.ShowIcon && !string.IsNullOrEmpty(metric.IconGlyph))
        {
            var icon = new TextBlock
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Text = metric.IconGlyph,
                Foreground = (Brush)converter.Convert(metric.DisplayColor, typeof(Brush), null, null!)!,
                Margin = new Thickness(0, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            sp.Children.Add(icon);
        }

        if (metric.ShowLabel && !string.IsNullOrEmpty(metric.Label))
        {
            var label = new TextBlock
            {
                Text = metric.Label,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)converter.Convert(metric.DisplayColor, typeof(Brush), null, null!)!,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            sp.Children.Add(label);
            _labelTextBlocks.Add(label);
        }

        var value = new TextBlock
        {
            Text = metric.Value,
            FontWeight = FontWeights.Medium,
            Foreground = (Brush)converter.Convert(metric.DisplayColor, typeof(Brush), null, null!)!,
            VerticalAlignment = VerticalAlignment.Center,
        };
        sp.Children.Add(value);

        value.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = metric });

        return sp;
    }

    public void TryEmbed()
    {
        if (_hwnd == IntPtr.Zero || _isDisposed) return;

        if (_isEmbedded && !_isOverlayMode && _placementService.HasValidAttachment(_hwnd))
        {
            EnsureWindowVisible();
            Reposition();
            return;
        }

        if (EmbedAsChild())
        {
            return;
        }

        if (!_isEmbedded || !_isOverlayMode)
        {
            EmbedAsOverlay();
        }

        _embedRetryCount++;
        ScheduleEmbedRetry();
    }

    private bool EmbedAsChild()
    {
        // Win10 子窗口模式：直接使用物理像素
        var visibleCount = _metrics.Count(m => m.IsVisible);
        var physicalWidth = CalculateWidgetWidthPhysical(visibleCount);
        var (_, tbHeight) = _platformDetector.GetTaskbarSize();
        var physicalHeight = tbHeight > 0 ? tbHeight : 48;

        if (!_placementService.EmbedWidget(_hwnd, physicalWidth, physicalHeight))
        {
            return false;
        }

        ApplyChildWindowStyles();
        _isEmbedded = true;
        _isOverlayMode = false;
        _embedRetryCount = 0;
        _embedRetryTimer.Stop();
        StartZOrderTimer();

        Reposition();
        if (CanShowForCurrentPosition())
        {
            EnsureWindowVisible();
            RevealWindow();
        }
        return true;
    }

    private void EmbedAsOverlay()
    {
        // Win11 覆盖模式：完全通过 Win32 管理 z-order，不使用 Window.Topmost

        ApplyOverlayWindowStyles();
        UpdateOverlaySize();
        UpdateOverlayPosition();
        EnsureWindowVisible();
        RevealWindow();

        _isEmbedded = true;
        _isOverlayMode = true;
        _overlayHealthTick = 0;
        _embedRetryCount = 0;
        _embedRetryTimer.Stop();
        StartZOrderTimer();
    }

    /// <summary>
    /// 启动后台线程定时器，每 500ms 通过 Win32 断言置顶 z-order。
    /// 使用 System.Threading.Timer 而非 DispatcherTimer，
    /// 确保在主窗口隐藏/失活时仍能可靠执行。
    /// </summary>
    private void StartZOrderTimer()
    {
        StopZOrderTimer();
        _zorderTimer = new System.Threading.Timer(ZOrderCallback, null, 500, 500);
    }

    private void StopZOrderTimer()
    {
        _zorderTimer?.Dispose();
        _zorderTimer = null;
    }

    private void ZOrderCallback(object? state)
    {
        if (_isDisposed || _hwnd == IntPtr.Zero) return;

        try
        {
            _window?.Dispatcher?.BeginInvoke(DispatcherPriority.Send, new Action(() =>
            {
                if (_isDisposed || _hwnd == IntPtr.Zero) return;

                if (!_isEmbedded)
                {
                    TryEmbed();
                    return;
                }

                if (_isOverlayMode)
                {
                    EnsureOverlayHealthy();
                    return;
                }

                if (!_placementService.HasValidAttachment(_hwnd))
                {
                    _placementService.DetachWidget();
                    _isEmbedded = false;
                    ResetStartButtonStability();
                    ScheduleEmbedRetry();
                    TryEmbed();
                    return;
                }

                if (_currentPosition == TaskbarPositionNames.RightOfStart && _window is not null && _window.Opacity < 1)
                {
                    Reposition();
                }

                if (CanShowForCurrentPosition() && !TaskbarNativeInterop.IsWindowVisible(_hwnd))
                {
                    EnsureWindowVisible();
                }

                RevealWindow();
            }));
        }
        catch
        {
            // Dispatcher 可能已关闭
        }
    }

    /// <summary>
    /// 更新 WPF 窗口尺寸（DIP），让内容正确布局。
    /// </summary>
    private void UpdateOverlaySize()
    {
        if (_window == null) return;

        var visibleCount = _metrics.Count(m => m.IsVisible);
        var maxPhysical = GetAvailableWidthPhysical();
        var maxDip = PhysicalToDip(maxPhysical);
        var dipWidth = CalculateWidgetWidthDip(visibleCount, maxDip);
        var (_, tbHeight) = _platformDetector.GetTaskbarSize();
        var dipHeight = PhysicalToDip(tbHeight > 0 ? tbHeight : 48);

        _window.Width = dipWidth;
        _window.Height = dipHeight;
        _window.UpdateLayout();
    }

    /// <summary>
    /// 根据当前配置计算任务栏上的屏幕坐标（物理像素），
    /// 用 SetWindowPos(HWND_TOPMOST) 同时定位和断言置顶。
    /// </summary>
    private void UpdateOverlayPosition()
    {
        if (_window == null || _hwnd == IntPtr.Zero) return;

        var hTaskbar = _platformDetector.GetTaskbarHandle();
        if (hTaskbar == IntPtr.Zero) return;
        if (!TaskbarNativeInterop.GetWindowRect(hTaskbar, out var taskbarRect)) return;

        var taskbarEdge = _platformDetector.GetTaskbarEdge();

        // 获取当前窗口物理尺寸（用于定位计算）
        TaskbarNativeInterop.GetWindowRect(_hwnd, out var widgetRect);
        var widgetPhysWidth = widgetRect.Width;
        var widgetPhysHeight = widgetRect.Height;

        // 所有坐标计算在物理像素空间完成
        var screenX = 0;
        var screenY = 0;

        switch (_currentPosition)
        {
            case TaskbarPositionNames.RightOfStart:
                if (!TryGetStartButtonRect(hTaskbar, out var startRect)) return;
                CalcRightOfStart(taskbarRect, taskbarEdge, widgetPhysWidth, widgetPhysHeight, startRect, out screenX, out screenY);
                break;
            case TaskbarPositionNames.Leftmost:
                CalcLeftmost(taskbarRect, taskbarEdge, widgetPhysWidth, widgetPhysHeight, out screenX, out screenY);
                break;
            default:
                CalcLeftOfTray(taskbarRect, taskbarEdge, widgetPhysWidth, widgetPhysHeight, hTaskbar, out screenX, out screenY);
                break;
        }

        // 使用 SetWindowPos(HWND_TOPMOST) 同时定位 + 断言置顶
        TaskbarNativeInterop.SetWindowPos(_hwnd, TaskbarNativeInterop.HWND_TOPMOST,
            screenX, screenY, widgetPhysWidth, widgetPhysHeight,
            TaskbarNativeInterop.SWP_NOACTIVATE | TaskbarNativeInterop.SWP_SHOWWINDOW);
    }

    private void CalcRightOfStart(TaskbarNativeInterop.RECT taskbarRect, TaskbarEdge taskbarEdge,
        int width, int height, TaskbarNativeInterop.RECT startRect, out int screenX, out int screenY)
    {
        screenX = 0;
        screenY = 0;

        switch (taskbarEdge)
        {
            case TaskbarEdge.Bottom:
            case TaskbarEdge.Top:
                screenX = startRect.Right - taskbarRect.Left + 4;
                screenY = taskbarRect.Top + Math.Max(0, (taskbarRect.Height - height) / 2);
                break;
            case TaskbarEdge.Left:
            case TaskbarEdge.Right:
                screenX = taskbarRect.Left + Math.Max(0, (taskbarRect.Width - width) / 2);
                screenY = startRect.Bottom - taskbarRect.Top + 4;
                break;
        }
    }

    private bool TryGetStartButtonRect(IntPtr hTaskbar, out TaskbarNativeInterop.RECT startRect)
    {
        var startBtn = TaskbarNativeInterop.FindWindowEx(hTaskbar, IntPtr.Zero, "Start", null);
        if (startBtn != IntPtr.Zero && TaskbarNativeInterop.GetWindowRect(startBtn, out startRect))
        {
            return true;
        }

        startRect = default;
        return false;
    }

    private void ResetStartButtonStability()
    {
        _lastStartButtonRect = default;
        _stableStartButtonSamples = 0;
    }

    private bool IsStartButtonAnchorReady(IntPtr hTaskbar)
    {
        if (!TryGetStartButtonRect(hTaskbar, out var startRect))
        {
            ResetStartButtonStability();
            return false;
        }

        if (_window?.Opacity >= 1)
        {
            _lastStartButtonRect = startRect;
            _stableStartButtonSamples = StartButtonStableSampleTarget;
            return true;
        }

        if (_stableStartButtonSamples > 0 && AreRectsEqual(_lastStartButtonRect, startRect))
        {
            _stableStartButtonSamples++;
        }
        else
        {
            _stableStartButtonSamples = 1;
        }

        _lastStartButtonRect = startRect;
        return _stableStartButtonSamples >= StartButtonStableSampleTarget;
    }

    private static bool AreRectsEqual(TaskbarNativeInterop.RECT left, TaskbarNativeInterop.RECT right)
    {
        return left.Left == right.Left &&
               left.Top == right.Top &&
               left.Right == right.Right &&
               left.Bottom == right.Bottom;
    }

    private void CalcLeftmost(TaskbarNativeInterop.RECT taskbarRect, TaskbarEdge taskbarEdge,
        int width, int height, out int screenX, out int screenY)
    {
        screenX = 0;
        screenY = 0;

        switch (taskbarEdge)
        {
            case TaskbarEdge.Bottom:
            case TaskbarEdge.Top:
                screenX = taskbarRect.Left + 4;
                screenY = taskbarRect.Top + Math.Max(0, (taskbarRect.Height - height) / 2);
                break;
            case TaskbarEdge.Left:
            case TaskbarEdge.Right:
                screenX = taskbarRect.Left + Math.Max(0, (taskbarRect.Width - width) / 2);
                screenY = taskbarRect.Top + 4;
                break;
        }
    }

    private void CalcLeftOfTray(TaskbarNativeInterop.RECT taskbarRect, TaskbarEdge taskbarEdge,
        int width, int height, IntPtr hTaskbar, out int screenX, out int screenY)
    {
        screenX = 0;
        screenY = 0;

        var trayNotify = TaskbarNativeInterop.FindWindowEx(hTaskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        int anchorX;

        if (trayNotify != IntPtr.Zero && TaskbarNativeInterop.GetWindowRect(trayNotify, out var trayRect))
        {
            anchorX = trayRect.Left;
        }
        else
        {
            anchorX = taskbarEdge == TaskbarEdge.Right
                ? taskbarRect.Left
                : taskbarRect.Right - 300;
        }

        switch (taskbarEdge)
        {
            case TaskbarEdge.Bottom:
            case TaskbarEdge.Top:
                screenX = anchorX - width;
                screenY = taskbarRect.Top + Math.Max(0, (taskbarRect.Height - height) / 2);
                break;
            case TaskbarEdge.Left:
                screenX = taskbarRect.Left + Math.Max(0, (taskbarRect.Width - width) / 2);
                screenY = anchorX - height;
                break;
            case TaskbarEdge.Right:
                screenX = taskbarRect.Left + Math.Max(0, (taskbarRect.Width - width) / 2);
                screenY = anchorX - height;
                break;
        }
    }

    /// <summary>
    /// 计算当前任务栏位置下，小组件可用的最大物理像素宽度。
    /// 复用已有的 TrayNotifyWnd / Start 按钮查找逻辑。
    /// </summary>
    private int GetAvailableWidthPhysical()
    {
        var hTaskbar = _platformDetector.GetTaskbarHandle();
        if (hTaskbar == IntPtr.Zero) return int.MaxValue;
        if (!TaskbarNativeInterop.GetWindowRect(hTaskbar, out var taskbarRect)) return int.MaxValue;

        var taskbarEdge = _platformDetector.GetTaskbarEdge();

        // 垂直任务栏：宽度受任务栏自身宽度限制
        if (taskbarEdge is TaskbarEdge.Left or TaskbarEdge.Right)
        {
            return Math.Max(60, taskbarRect.Width - 8);
        }

        // 水平任务栏：根据位置模式计算左右边界
        int rightBound;
        var trayNotify = TaskbarNativeInterop.FindWindowEx(hTaskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (trayNotify != IntPtr.Zero && TaskbarNativeInterop.GetWindowRect(trayNotify, out var trayRect))
        {
            rightBound = trayRect.Left;
        }
        else
        {
            rightBound = taskbarRect.Right - 300;
        }

        switch (_currentPosition)
        {
            case TaskbarPositionNames.RightOfStart:
            {
                if (!TryGetStartButtonRect(hTaskbar, out var startRect))
                    return Math.Max(60, rightBound - taskbarRect.Left - 8);

                return Math.Max(60, rightBound - startRect.Right - 4);
            }

            case TaskbarPositionNames.Leftmost:
            {
                return Math.Max(60, rightBound - taskbarRect.Left - 8);
            }

            default: // LeftOfTray
            {
                // 可用空间 = 托盘左边到任务栏左边，减去开始按钮区域
                int leftOffset = 0;
                if (TryGetStartButtonRect(hTaskbar, out var startRect))
                {
                    leftOffset = startRect.Right - taskbarRect.Left;
                }
                return Math.Max(60, rightBound - taskbarRect.Left - leftOffset);
            }
        }
    }

    private void Reposition()
    {
        if (!_isEmbedded || _isDisposed) return;

        if (_isOverlayMode)
        {
            UpdateOverlaySize();
            UpdateOverlayPosition();
        }
        else
        {
            var visibleCount = _metrics.Count(m => m.IsVisible);
            var physicalWidth = CalculateWidgetWidthPhysical(visibleCount);
            var (_, tbHeight) = _platformDetector.GetTaskbarSize();
            var physicalHeight = tbHeight > 0 ? tbHeight : 48;
            UpdateEmbeddedWindowSize(physicalWidth, physicalHeight);

            if (_placementService.Platform == TaskbarPlatform.Win11Modern)
            {
                UpdateEmbeddedChildBounds(physicalWidth, physicalHeight);
            }
            else
            {
                TaskbarNativeInterop.MoveWindow(_hwnd, 0, 0, physicalWidth, physicalHeight, true);
                _placementService.UpdateSize(physicalWidth, physicalHeight);
            }
        }
    }

    private void UpdateEmbeddedWindowSize(int physicalWidth, int physicalHeight)
    {
        if (_window == null) return;

        _window.Width = PhysicalToDip(physicalWidth);
        _window.Height = PhysicalToDip(physicalHeight);
        _window.UpdateLayout();
    }

    private void EnsureOverlayHealthy(bool forceTopmost = true)
    {
        if (_hwnd == IntPtr.Zero) return;

        if (!TryGetOverlayBounds(out var screenX, out var screenY, out var width, out var height))
        {
            return;
        }

        if (!TaskbarNativeInterop.IsWindowVisible(_hwnd))
        {
            SetOverlayWindowPos(screenX, screenY, width, height, showWindow: true);
            _overlayHealthTick = 0;
            return;
        }

        var needsReposition = !TaskbarNativeInterop.GetWindowRect(_hwnd, out var currentRect) ||
                              currentRect.Left != screenX ||
                              currentRect.Top != screenY ||
                              currentRect.Width != width ||
                              currentRect.Height != height;

        if (needsReposition)
        {
            SetOverlayWindowPos(screenX, screenY, width, height, showWindow: true);
            _overlayHealthTick = 0;
            return;
        }

        if (!forceTopmost)
        {
            return;
        }

        _overlayHealthTick = (_overlayHealthTick + 1) % 4;
        if (_overlayHealthTick == 0)
        {
            TaskbarNativeInterop.SetWindowPos(_hwnd, TaskbarNativeInterop.HWND_TOPMOST,
                0, 0, 0, 0,
                TaskbarNativeInterop.SWP_NOMOVE | TaskbarNativeInterop.SWP_NOSIZE | TaskbarNativeInterop.SWP_NOACTIVATE);
        }
    }

    private bool TryGetOverlayBounds(out int screenX, out int screenY, out int width, out int height)
    {
        screenX = 0;
        screenY = 0;
        width = 0;
        height = 0;

        if (_window == null || _hwnd == IntPtr.Zero) return false;

        if (!TaskbarNativeInterop.GetWindowRect(_hwnd, out var widgetRect))
        {
            return false;
        }

        width = widgetRect.Width;
        height = widgetRect.Height;

        return TryGetTaskbarScreenBounds(width, height, out screenX, out screenY);
    }

    private bool TryGetTaskbarScreenBounds(int width, int height, out int screenX, out int screenY)
    {
        screenX = 0;
        screenY = 0;

        var hTaskbar = _platformDetector.GetTaskbarHandle();
        if (hTaskbar == IntPtr.Zero) return false;
        if (!TaskbarNativeInterop.GetWindowRect(hTaskbar, out var taskbarRect)) return false;

        var taskbarEdge = _platformDetector.GetTaskbarEdge();

        switch (_currentPosition)
        {
            case TaskbarPositionNames.RightOfStart:
                if (!TryGetStartButtonRect(hTaskbar, out var startRect))
                {
                    return false;
                }

                CalcRightOfStart(taskbarRect, taskbarEdge, width, height, startRect, out screenX, out screenY);
                break;
            case TaskbarPositionNames.Leftmost:
                CalcLeftmost(taskbarRect, taskbarEdge, width, height, out screenX, out screenY);
                break;
            default:
                CalcLeftOfTray(taskbarRect, taskbarEdge, width, height, hTaskbar, out screenX, out screenY);
                break;
        }

        return true;
    }

    private void UpdateEmbeddedChildBounds(int width, int height)
    {
        var hTaskbar = _platformDetector.GetTaskbarHandle();
        if (hTaskbar == IntPtr.Zero) return;
        if (!TaskbarNativeInterop.GetWindowRect(hTaskbar, out var taskbarRect)) return;
        if (!TryGetTaskbarScreenBounds(width, height, out var screenX, out var screenY)) return;

        var childX = screenX - taskbarRect.Left;
        var childY = screenY - taskbarRect.Top;

        TaskbarNativeInterop.MoveWindow(_hwnd, childX, childY, width, height, true);
    }

    private void SetOverlayWindowPos(int screenX, int screenY, int width, int height, bool showWindow)
    {
        var flags = TaskbarNativeInterop.SWP_NOACTIVATE;
        if (showWindow)
        {
            flags |= TaskbarNativeInterop.SWP_SHOWWINDOW;
        }

        TaskbarNativeInterop.SetWindowPos(_hwnd, TaskbarNativeInterop.HWND_TOPMOST,
            screenX, screenY, width, height, flags);
    }

    private void ApplyChildWindowStyles()
    {
        var style = TaskbarNativeInterop.GetWindowLong(_hwnd, TaskbarNativeInterop.GWL_STYLE);
        style = unchecked((int)((uint)style | TaskbarNativeInterop.WS_CHILD | TaskbarNativeInterop.WS_VISIBLE |
                                 TaskbarNativeInterop.WS_CLIPSIBLINGS | TaskbarNativeInterop.WS_CLIPCHILDREN));
        style = unchecked((int)((uint)style & ~(TaskbarNativeInterop.WS_POPUP | TaskbarNativeInterop.WS_CAPTION)));
        TaskbarNativeInterop.SetWindowLong(_hwnd, TaskbarNativeInterop.GWL_STYLE, style);

        var exStyle = TaskbarNativeInterop.GetWindowLong(_hwnd, TaskbarNativeInterop.GWL_EXSTYLE);
        exStyle = unchecked((int)((uint)exStyle | TaskbarNativeInterop.WS_EX_TRANSPARENT | TaskbarNativeInterop.WS_EX_NOACTIVATE));
        exStyle = unchecked((int)((uint)exStyle & ~(TaskbarNativeInterop.WS_EX_TOPMOST | TaskbarNativeInterop.WS_EX_TOOLWINDOW)));
        TaskbarNativeInterop.SetWindowLong(_hwnd, TaskbarNativeInterop.GWL_EXSTYLE, exStyle);
    }

    private void ApplyOverlayWindowStyles()
    {
        var style = TaskbarNativeInterop.GetWindowLong(_hwnd, TaskbarNativeInterop.GWL_STYLE);
        style = unchecked((int)((uint)style | TaskbarNativeInterop.WS_POPUP | TaskbarNativeInterop.WS_VISIBLE));
        style = unchecked((int)((uint)style & ~(TaskbarNativeInterop.WS_CHILD | TaskbarNativeInterop.WS_CAPTION)));
        TaskbarNativeInterop.SetWindowLong(_hwnd, TaskbarNativeInterop.GWL_STYLE, style);

        var exStyle = TaskbarNativeInterop.GetWindowLong(_hwnd, TaskbarNativeInterop.GWL_EXSTYLE);
        exStyle |= (int)(TaskbarNativeInterop.WS_EX_TOPMOST |
                         TaskbarNativeInterop.WS_EX_TRANSPARENT |
                         TaskbarNativeInterop.WS_EX_NOACTIVATE |
                         TaskbarNativeInterop.WS_EX_TOOLWINDOW);
        TaskbarNativeInterop.SetWindowLong(_hwnd, TaskbarNativeInterop.GWL_EXSTYLE, exStyle);
    }

    private void EnsureWindowVisible()
    {
        if (_hwnd == IntPtr.Zero || TaskbarNativeInterop.IsWindowVisible(_hwnd)) return;
        TaskbarNativeInterop.ShowWindow(_hwnd, TaskbarNativeInterop.SW_SHOWNOACTIVATE);
    }

    private void RevealWindow()
    {
        if (_window is null || _window.Opacity >= 1) return;
        if (!CanShowForCurrentPosition()) return;
        _window.Opacity = 1;
    }

    private bool CanShowForCurrentPosition()
    {
        if (_currentPosition != TaskbarPositionNames.RightOfStart)
        {
            return true;
        }

        var hTaskbar = _platformDetector.GetTaskbarHandle();
        return hTaskbar != IntPtr.Zero && IsStartButtonAnchorReady(hTaskbar);
    }

    private void ScheduleEmbedRetry()
    {
        if (_isDisposed) return;

        if (!_embedRetryTimer.IsEnabled && _embedRetryCount < MaxEmbedRetries)
        {
            _embedRetryTimer.Start();
        }
    }

    /// <summary>
    /// 计算窗口宽度（DIP 单位），用于 WPF 内容布局。
    /// maxDipWidth 为可用空间上限（DIP），超出时自动压缩。
    /// </summary>
    private double CalculateWidgetWidthDip(int visibleMetricCount, double maxDipWidth = double.PositiveInfinity)
    {
        if (visibleMetricCount == 0) return 60;
        if (_rootGrid == null) return 60;

        var (_, tbHeight) = _platformDetector.GetTaskbarSize();
        var availableHeightDip = PhysicalToDip(tbHeight > 0 ? tbHeight : 48);

        // 阶段 0：自然宽度，正常间距
        ApplyCompressionMode(CompressionMode.None);
        _rootGrid.Measure(new Size(double.PositiveInfinity, Math.Max(availableHeightDip, 1)));
        var naturalWidth = Math.Max(60, Math.Ceiling(_rootGrid.DesiredSize.Width) + 8);

        if (naturalWidth <= maxDipWidth)
        {
            return naturalWidth;
        }

        // 阶段 1：缩小列间距
        ApplyCompressionMode(CompressionMode.ReducedPadding);
        _rootGrid.Measure(new Size(double.PositiveInfinity, Math.Max(availableHeightDip, 1)));
        var reducedWidth = Math.Max(60, Math.Ceiling(_rootGrid.DesiredSize.Width) + 8);

        if (reducedWidth <= maxDipWidth)
        {
            return reducedWidth;
        }

        // 阶段 2：去除间距 + 标签截断
        ApplyCompressionMode(CompressionMode.TrimLabels);
        _rootGrid.Measure(new Size(maxDipWidth, Math.Max(availableHeightDip, 1)));
        return maxDipWidth;
    }

    /// <summary>
    /// 计算窗口宽度（物理像素），用于 Win32 MoveWindow / SetParent。
    /// </summary>
    private int CalculateWidgetWidthPhysical(int visibleMetricCount)
    {
        var maxPhysical = GetAvailableWidthPhysical();
        var maxDip = _hwnd == IntPtr.Zero ? double.PositiveInfinity : PhysicalToDip(maxPhysical);
        var dipWidth = CalculateWidgetWidthDip(visibleMetricCount, maxDip);
        if (_hwnd == IntPtr.Zero) return (int)dipWidth;
        var source = HwndSource.FromHwnd(_hwnd);
        if (source?.CompositionTarget == null) return (int)dipWidth;
        return (int)Math.Ceiling(dipWidth / source.CompositionTarget.TransformFromDevice.M11);
    }

    private enum CompressionMode
    {
        None,
        ReducedPadding,
        TrimLabels
    }

    /// <summary>
    /// 根据压缩级别调整列间距和标签截断。
    /// </summary>
    private void ApplyCompressionMode(CompressionMode mode)
    {
        var columnMargin = mode switch
        {
            CompressionMode.ReducedPadding => new Thickness(1, 0, 1, 0),
            CompressionMode.TrimLabels => new Thickness(0, 0, 0, 0),
            _ => new Thickness(6, 0, 6, 0)
        };

        foreach (var panel in _columnPanels)
        {
            panel.Margin = columnMargin;
        }

        var shouldTrim = mode == CompressionMode.TrimLabels;
        foreach (var label in _labelTextBlocks)
        {
            if (shouldTrim)
            {
                label.TextTrimming = TextTrimming.CharacterEllipsis;
                label.Margin = new Thickness(0, 0, 2, 0);
            }
            else
            {
                label.TextTrimming = TextTrimming.None;
                label.Margin = new Thickness(0, 0, 4, 0);
            }
        }
    }

    public void ApplySnapshot(HardwareMetrics snapshot, UiSettings settings, IReadOnlyDictionary<string, string> texts)
    {
        if (_isDisposed) return;

        foreach (var metric in _metrics)
        {
            if (!metric.IsVisible) continue;
            metric.Value = ExtractMetricValue(metric.MetricKey, snapshot, settings, texts);
        }

        RefreshWindowSizeIfNeeded();
    }

    private void RefreshWindowSizeIfNeeded()
    {
        if (!_isEmbedded || _isDisposed || _hwnd == IntPtr.Zero) return;

        var visibleCount = _metrics.Count(m => m.IsVisible);
        var desiredWidth = CalculateWidgetWidthPhysical(visibleCount);
        var (_, tbHeight) = _platformDetector.GetTaskbarSize();
        var desiredHeight = tbHeight > 0 ? tbHeight : 48;

        if (!TaskbarNativeInterop.GetWindowRect(_hwnd, out var currentRect))
        {
            Reposition();
            return;
        }

        if (currentRect.Width != desiredWidth || currentRect.Height != desiredHeight)
        {
            Reposition();
        }
    }

    public void ApplyMetricsFromSettings(ObservableCollection<OverlayMetricViewModel> sourceMetrics)
    {
        if (_isDisposed) return;

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
            _metrics.RemoveAt(_metrics.Count - 1);

        RebuildLayout();

        if (_isEmbedded)
            Reposition();
    }

    public void ApplyStyle(UiSettings settings)
    {
        if (_isDisposed) return;

        var previousPosition = _currentPosition;
        _currentFontSize = Math.Max(8, Math.Min(20, settings.TaskbarFontSize));
        _currentMaxRows = Math.Max(1, settings.TaskbarMaxRows);
        _currentPosition = settings.TaskbarPosition;

        if (!string.Equals(previousPosition, _currentPosition, StringComparison.Ordinal))
        {
            ResetStartButtonStability();
            if (_window is not null && string.Equals(_currentPosition, TaskbarPositionNames.RightOfStart, StringComparison.Ordinal))
            {
                _window.Opacity = 0;
            }
        }

        if (_rootGrid != null)
        {
            _rootGrid.SetValue(TextElement.FontSizeProperty, (double)_currentFontSize);
        }

        RebuildLayout();

        if (_isEmbedded)
            Reposition();

        RevealWindow();
    }

    public void SetRowSpacing(int spacing)
    {
        _currentRowSpacing = spacing;
        RebuildLayout();
    }

    private void OnEmbedRetry(object? sender, EventArgs e)
    {
        if (_isDisposed) { _embedRetryTimer.Stop(); return; }
        if (_isEmbedded && !_isOverlayMode) { _embedRetryTimer.Stop(); return; }
        if (_embedRetryCount >= MaxEmbedRetries) { _embedRetryTimer.Stop(); return; }
        TryEmbed();
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

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _embedRetryTimer.Stop();
        StopZOrderTimer();

        if (_hwnd != IntPtr.Zero)
        {
            TaskbarNativeInterop.ShowWindow(_hwnd, TaskbarNativeInterop.SW_HIDE);
        }

        if (!_isOverlayMode)
        {
            _placementService.DetachWidget();
        }

        if (_window != null)
        {
            _window.SourceInitialized -= OnSourceInitialized;
            _window.Close();
            _window = null;
        }

        _hwnd = IntPtr.Zero;
    }
}
