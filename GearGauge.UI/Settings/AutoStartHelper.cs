using Microsoft.Win32;

namespace GearGauge.UI.Settings;

public static class AutoStartHelper
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "GearGauge";

    public static void Enable()
    {
        var exePath = Environment.ProcessPath
                      ?? AppDomain.CurrentDomain.BaseDirectory + "GearGauge.UI.exe";
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(AppName, $"\"{exePath}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    public static void Apply(bool autoStart)
    {
        if (autoStart) Enable();
        else Disable();
    }
}
