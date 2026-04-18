using System;

namespace GearGauge.UI.Taskbar;

/// <summary>
/// 负责将小组件窗口嵌入 Windows 任务栏。
/// Win10: 通过 SetParent 嵌入 ReBarWindow32，缩小 MSTaskSwWClass 腾出空间。
/// Win11: 通过 SetParent 嵌入 Shell_TrayWnd，定位到合适的显示位置。
/// </summary>
internal sealed class TaskbarPlacementService
{
    private readonly TaskbarPlatformDetector _detector = new();

    // Win10 经典任务栏窗口句柄
    private IntPtr _reBarWindow;
    private IntPtr _mstaskSwWClass;
    private int _originalMstaskWidth;
    private int _originalMstaskX;

    // Win11 任务栏
    private IntPtr _shellTrayWnd;

    // 小组件信息
    private IntPtr _widgetHwnd;
    private int _widgetWidth;
    private int _widgetHeight;
    private TaskbarPlatform _platform;
    private bool _isEmbedded;

    public bool IsEmbedded => _isEmbedded;
    public TaskbarPlatform Platform => _platform;

    /// <summary>
    /// 注册 TaskbarCreated 消息，用于监听 Explorer 重启。
    /// </summary>
    public uint GetTaskbarCreatedMessage()
    {
        return TaskbarNativeInterop.RegisterWindowMessage("TaskbarCreated");
    }

    /// <summary>
    /// 将小组件窗口嵌入任务栏。返回是否成功。
    /// </summary>
    public bool EmbedWidget(IntPtr widgetHwnd, int width, int height)
    {
        if (widgetHwnd == IntPtr.Zero) return false;

        _widgetHwnd = widgetHwnd;
        _widgetWidth = width;
        _widgetHeight = height;
        _platform = _detector.DetectPlatform();

        return _platform == TaskbarPlatform.Win11Modern
            ? EmbedWin11()
            : EmbedWin10();
    }

    /// <summary>
    /// Win10 嵌入：SetParent 到 ReBarWindow32，缩小 MSTaskSwWClass。
    /// 小组件放在任务栏最左侧。
    /// </summary>
    private bool EmbedWin10()
    {
        var hTaskbar = TaskbarNativeInterop.FindWindow("Shell_TrayWnd", null);
        if (hTaskbar == IntPtr.Zero) return false;

        _shellTrayWnd = hTaskbar;

        // Shell_TrayWnd → ReBarWindow32
        _reBarWindow = TaskbarNativeInterop.FindWindowEx(hTaskbar, IntPtr.Zero, "ReBarWindow32", null);
        if (_reBarWindow == IntPtr.Zero)
        {
            _reBarWindow = TaskbarNativeInterop.FindWindowEx(hTaskbar, IntPtr.Zero, "WorkerW", null);
        }
        if (_reBarWindow == IntPtr.Zero) return false;

        // ReBarWindow32 → MSTaskSwWClass
        _mstaskSwWClass = TaskbarNativeInterop.FindWindowEx(_reBarWindow, IntPtr.Zero, "MSTaskSwWClass", null);
        if (_mstaskSwWClass == IntPtr.Zero)
        {
            _mstaskSwWClass = TaskbarNativeInterop.FindWindowEx(_reBarWindow, IntPtr.Zero, "MSTaskListWClass", null);
        }

        if (_mstaskSwWClass != IntPtr.Zero)
        {
            TaskbarNativeInterop.GetWindowRect(_mstaskSwWClass, out var origRect);
            _originalMstaskWidth = origRect.Width;
            _originalMstaskX = 0;
        }

        var result = TaskbarNativeInterop.SetParent(_widgetHwnd, _reBarWindow);
        if (result == IntPtr.Zero) return false;

        // 缩小 MSTaskSwWClass 腾出空间
        if (_mstaskSwWClass != IntPtr.Zero)
        {
            var newMstaskWidth = Math.Max(0, _originalMstaskWidth - _widgetWidth);
            TaskbarNativeInterop.MoveWindow(_mstaskSwWClass, _widgetWidth, 0,
                newMstaskWidth, _widgetHeight, true);
        }

        // 小组件放在最左侧
        TaskbarNativeInterop.MoveWindow(_widgetHwnd, 0, 0,
            _widgetWidth, _widgetHeight, true);

        _isEmbedded = true;
        return true;
    }

    public bool HasValidAttachment(IntPtr widgetHwnd)
    {
        if (!_isEmbedded || widgetHwnd == IntPtr.Zero || !TaskbarNativeInterop.IsWindow(widgetHwnd))
            return false;

        var expectedParent = _platform == TaskbarPlatform.Win10Classic
            ? _reBarWindow
            : _shellTrayWnd;

        if (expectedParent == IntPtr.Zero || !TaskbarNativeInterop.IsWindow(expectedParent))
            return false;

        return TaskbarNativeInterop.GetParent(widgetHwnd) == expectedParent;
    }

    /// <summary>
    /// Win11 嵌入：SetParent 到 Shell_TrayWnd。
    /// 优先查找 TrayNotifyWnd（时钟区域），将小组件定位在其左侧。
    /// 如果找不到，则尝试定位在任务栏左半部分。
    /// </summary>
    private bool EmbedWin11()
    {
        var hTaskbar = TaskbarNativeInterop.FindWindow("Shell_TrayWnd", null);
        if (hTaskbar == IntPtr.Zero) return false;

        _shellTrayWnd = hTaskbar;

        var result = TaskbarNativeInterop.SetParent(_widgetHwnd, hTaskbar);
        if (result == IntPtr.Zero) return false;

        _isEmbedded = true;
        return true;
    }

    /// <summary>
    /// 计算 Win11 任务栏中的位置。
    /// 核心逻辑：找到 TrayNotifyWnd 的左边界，小组件紧贴其左侧放置。
    /// </summary>
    private void PositionWin11()
    {
        if (_shellTrayWnd == IntPtr.Zero) return;
        if (!TaskbarNativeInterop.GetWindowRect(_shellTrayWnd, out var taskbarRect))
            return;

        var taskbarWidth = taskbarRect.Width;
        var taskbarHeight = taskbarRect.Height;
        var y = Math.Max(0, (taskbarHeight - _widgetHeight) / 2);

        // 查找 TrayNotifyWnd（系统时钟/通知区域）
        var trayNotify = TaskbarNativeInterop.FindWindowEx(_shellTrayWnd, IntPtr.Zero, "TrayNotifyWnd", null);

        if (trayNotify != IntPtr.Zero &&
            TaskbarNativeInterop.GetWindowRect(trayNotify, out var trayRect))
        {
            // 转换到任务栏内坐标，放在 TrayNotifyWnd 正左侧
            var x = trayRect.Left - taskbarRect.Left - _widgetWidth;
            x = Math.Max(0, x);

            TaskbarNativeInterop.MoveWindow(_widgetHwnd, x, y,
                _widgetWidth, _widgetHeight, true);
        }
        else
        {
            // Fallback: 找不到 TrayNotifyWnd 时，放在任务栏右侧（留出系统托盘空间）
            var estimatedTrayWidth = 300;
            var x = taskbarWidth - _widgetWidth - estimatedTrayWidth;
            x = Math.Max(0, x);

            TaskbarNativeInterop.MoveWindow(_widgetHwnd, x, y,
                _widgetWidth, _widgetHeight, true);
        }
    }

    public void UpdatePosition()
    {
        if (_widgetHwnd == IntPtr.Zero || !_isEmbedded) return;

        if (_platform == TaskbarPlatform.Win10Classic)
        {
            TaskbarNativeInterop.MoveWindow(_widgetHwnd, 0, 0,
                _widgetWidth, _widgetHeight, true);
        }
        else
        {
            PositionWin11();
        }
    }

    public void UpdateSize(int width, int height)
    {
        _widgetWidth = width;
        _widgetHeight = height;

        if (_platform == TaskbarPlatform.Win10Classic && _mstaskSwWClass != IntPtr.Zero)
        {
            var newMstaskWidth = Math.Max(0, _originalMstaskWidth - _widgetWidth);
            TaskbarNativeInterop.MoveWindow(_mstaskSwWClass, _widgetWidth, 0,
                newMstaskWidth, _widgetHeight, true);
        }

        UpdatePosition();
    }

    public void DetachWidget()
    {
        if (!_isEmbedded) return;

        if (_platform == TaskbarPlatform.Win10Classic && _mstaskSwWClass != IntPtr.Zero)
        {
            TaskbarNativeInterop.MoveWindow(_mstaskSwWClass, _originalMstaskX, 0,
                _originalMstaskWidth, _widgetHeight, true);
        }

        if (_widgetHwnd != IntPtr.Zero && TaskbarNativeInterop.IsWindow(_widgetHwnd))
        {
            TaskbarNativeInterop.SetParent(_widgetHwnd, IntPtr.Zero);
        }

        _isEmbedded = false;
    }
}
