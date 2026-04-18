using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GearGauge.Hardware;

internal sealed record ForegroundWindowInfo(
    nint Handle,
    int ProcessId,
    string ProcessName,
    string WindowTitle,
    int Width,
    int Height,
    nint MonitorHandle);

internal static class ForegroundWindowReader
{
    public static ForegroundWindowInfo? TryGet()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        return TryCreate(handle);
    }

    public static ForegroundWindowInfo? TryGetByProcessId(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            var handle = process.MainWindowHandle;
            return handle == IntPtr.Zero ? null : TryCreate(handle, process);
        }
        catch
        {
            return null;
        }
    }

    private static string GetWindowTitle(nint handle)
    {
        var builder = new StringBuilder(512);
        return GetWindowText(handle, builder, builder.Capacity) > 0
            ? builder.ToString()
            : string.Empty;
    }

    private static ForegroundWindowInfo? TryCreate(nint handle, Process? existingProcess = null)
    {
        if (!IsWindowVisible(handle) || !GetWindowRect(handle, out var rect))
        {
            return null;
        }

        _ = GetWindowThreadProcessId(handle, out var rawProcessId);
        if (rawProcessId == 0)
        {
            return null;
        }

        try
        {
            using var process = existingProcess is null
                ? Process.GetProcessById((int)rawProcessId)
                : null;
            var targetProcess = existingProcess ?? process!;
            var title = GetWindowTitle(handle);
            var width = Math.Max(0, rect.Right - rect.Left);
            var height = Math.Max(0, rect.Bottom - rect.Top);

            if (width == 0 || height == 0)
            {
                return null;
            }

            return new ForegroundWindowInfo(
                handle,
                targetProcess.Id,
                targetProcess.ProcessName,
                title,
                width,
                height,
                MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST));
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hWnd, uint dwFlags);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
