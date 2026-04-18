using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GearGauge.UI.Taskbar;

internal enum TaskbarPlatform
{
    Win10Classic,
    Win11Modern
}

internal enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right
}

internal sealed class TaskbarPlatformDetector
{
    /// <summary>
    /// 检测当前任务栏平台。通过查找 Win11 特有的 XAML Composition Bridge 窗口来区分。
    /// 如果用户在 Win11 上使用了经典任务栏工具，会正确返回 Win10Classic。
    /// </summary>
    public TaskbarPlatform DetectPlatform()
    {
        var hTaskbar = TaskbarNativeInterop.FindWindow("Shell_TrayWnd", null);
        if (hTaskbar == IntPtr.Zero)
            return TaskbarPlatform.Win10Classic;

        var contentBridge = TaskbarNativeInterop.FindWindowEx(
            hTaskbar, IntPtr.Zero,
            "Windows.UI.Composition.DesktopWindowContentBridge", null);

        return contentBridge != IntPtr.Zero
            ? TaskbarPlatform.Win11Modern
            : TaskbarPlatform.Win10Classic;
    }

    /// <summary>
    /// 获取任务栏所在屏幕边缘。
    /// </summary>
    public TaskbarEdge GetTaskbarEdge()
    {
        var hTaskbar = TaskbarNativeInterop.FindWindow("Shell_TrayWnd", null);
        if (hTaskbar == IntPtr.Zero)
            return TaskbarEdge.Bottom;

        if (!TaskbarNativeInterop.GetWindowRect(hTaskbar, out var taskbarRect))
            return TaskbarEdge.Bottom;

        var monitor = TaskbarNativeInterop.MonitorFromWindow(hTaskbar, TaskbarNativeInterop.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return TaskbarEdge.Bottom;

        var mi = new TaskbarNativeInterop.MONITORINFO
        {
            cbSize = Marshal.SizeOf<TaskbarNativeInterop.MONITORINFO>()
        };
        if (!TaskbarNativeInterop.GetMonitorInfo(monitor, ref mi))
            return TaskbarEdge.Bottom;

        var screenW = mi.rcMonitor.Width;
        var screenH = mi.rcMonitor.Height;
        var tbW = taskbarRect.Width;
        var tbH = taskbarRect.Height;

        // 水平任务栏（宽度接近屏幕宽度）
        if (tbW >= screenW)
            return taskbarRect.Top < mi.rcMonitor.Top + (screenH / 2)
                ? TaskbarEdge.Top
                : TaskbarEdge.Bottom;

        // 垂直任务栏
        return taskbarRect.Left < mi.rcMonitor.Left + (screenW / 2)
            ? TaskbarEdge.Left
            : TaskbarEdge.Right;
    }

    /// <summary>
    /// 检测 Win11 是否启用了居中任务栏图标。
    /// 读取注册表 TaskbarAl 值: 1=居中, 0=左对齐。
    /// </summary>
    public bool IsWin11CenteredIcons()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            var value = key?.GetValue("TaskbarAl");
            return value is int intValue && intValue == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取任务栏窗口句柄。
    /// </summary>
    public IntPtr GetTaskbarHandle()
    {
        return TaskbarNativeInterop.FindWindow("Shell_TrayWnd", null);
    }

    /// <summary>
    /// 获取任务栏尺寸（像素）。
    /// </summary>
    public (int Width, int Height) GetTaskbarSize()
    {
        var hTaskbar = TaskbarNativeInterop.FindWindow("Shell_TrayWnd", null);
        if (hTaskbar == IntPtr.Zero)
            return (0, 40);

        TaskbarNativeInterop.GetWindowRect(hTaskbar, out var rect);
        return (rect.Width, rect.Height);
    }

    /// <summary>
    /// 获取任务栏在屏幕上的矩形区域。
    /// </summary>
    public bool GetTaskbarRect(out TaskbarNativeInterop.RECT rect)
    {
        var hTaskbar = TaskbarNativeInterop.FindWindow("Shell_TrayWnd", null);
        if (hTaskbar == IntPtr.Zero)
        {
            rect = default;
            return false;
        }

        return TaskbarNativeInterop.GetWindowRect(hTaskbar, out rect);
    }
}
