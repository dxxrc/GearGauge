using System;
using System.Windows;
using System.Windows.Interop;
using GearGauge.UI.Localization;
using GearGauge.UI.Settings;
using GearGauge.UI.Taskbar;
using Hardcodet.Wpf.TaskbarNotification;

namespace GearGauge.UI;

public partial class App : Application
{
    private TaskbarIcon? _notifyIcon;
    private bool _isQuitting;
    private IReadOnlyDictionary<string, string> _texts = new Dictionary<string, string>();

    private const int CmdShowWindow = 1;
    private const int CmdOverlayToggle = 2;
    private const int CmdTaskbarToggle = 3;
    private const int CmdExit = 4;

    public bool IsQuitting => _isQuitting;

    public void BeginShutdown()
    {
        _isQuitting = true;
        CleanupTrayIcon();
        Shutdown();
    }

    /// <summary>
    /// 由 MainWindow 调用，同步语言文本。
    /// 配置项不再通过此方法同步，菜单弹出时直接从持久化存储读取。
    /// </summary>
    public void UpdateTraySettings(UiSettings settings, IReadOnlyDictionary<string, string> texts)
    {
        _texts = texts;

        if (_notifyIcon is not null)
            _notifyIcon.Visibility = Visibility.Visible;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = new UiSettingsStore().Load();
        var localizer = new UiLocalizer();
        _texts = localizer.GetTexts(settings.Language);

        _notifyIcon = new TaskbarIcon();
        _notifyIcon.ToolTipText = "GearGauge";
        _notifyIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/assets/branding/GearGauge.ico"));
        _notifyIcon.Visibility = Visibility.Visible;
        _notifyIcon.TrayLeftMouseDown += OnTrayLeftMouseDown;
        _notifyIcon.TrayRightMouseDown += OnTrayRightMouseDown;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CleanupTrayIcon();
        base.OnExit(e);
    }

    private void CleanupTrayIcon()
    {
        if (_notifyIcon is null) return;
        _notifyIcon.TrayLeftMouseDown -= OnTrayLeftMouseDown;
        _notifyIcon.TrayRightMouseDown -= OnTrayRightMouseDown;
        _notifyIcon.Visibility = Visibility.Collapsed;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private void OnTrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }

    private void OnTrayRightMouseDown(object sender, RoutedEventArgs e)
    {
        ShowTrayPopupMenu();
    }

    /// <summary>
    /// 使用 Win32 TrackPopupMenu 显示原生右键菜单。
    /// 每次弹出时直接从持久化存储读取配置，确保勾选状态始终与实际一致。
    /// </summary>
    private void ShowTrayPopupMenu()
    {
        var settings = new UiSettingsStore().Load();

        var hMenu = TaskbarNativeInterop.CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            uint pos = 0;
            var mfStr = TaskbarNativeInterop.MF_STRING;
            var mfSep = TaskbarNativeInterop.MF_SEPARATOR;
            var mfChk = TaskbarNativeInterop.MF_CHECKED;

            TaskbarNativeInterop.InsertMenu(hMenu, pos++, mfStr, (IntPtr)CmdShowWindow, GetText("TrayShowWindow"));
            TaskbarNativeInterop.InsertMenu(hMenu, pos++, mfSep, IntPtr.Zero, null);
            TaskbarNativeInterop.InsertMenu(hMenu, pos++, mfStr | (settings.OverlayEnabled ? mfChk : 0), (IntPtr)CmdOverlayToggle, GetText("TrayOverlayToggle"));
            TaskbarNativeInterop.InsertMenu(hMenu, pos++, mfStr | (settings.TaskbarWidgetEnabled ? mfChk : 0), (IntPtr)CmdTaskbarToggle, GetText("TrayTaskbarToggle"));
            TaskbarNativeInterop.InsertMenu(hMenu, pos++, mfSep, IntPtr.Zero, null);
            TaskbarNativeInterop.InsertMenu(hMenu, pos++, mfStr, (IntPtr)CmdExit, GetText("TrayExit"));

            TaskbarNativeInterop.GetCursorPos(out var pt);
            var hwnd = GetMenuHwnd();

            // SetForegroundWindow 确保 TrackPopupMenu 能正确关闭
            TaskbarNativeInterop.SetForegroundWindow(hwnd);

            var cmd = TaskbarNativeInterop.TrackPopupMenu(
                hMenu,
                TaskbarNativeInterop.TPM_LEFTALIGN | TaskbarNativeInterop.TPM_RETURNCMD | TaskbarNativeInterop.TPM_NONOTIFY,
                pt.X, pt.Y, 0, hwnd, IntPtr.Zero);

            HandleMenuCommand(cmd, settings);
        }
        finally
        {
            TaskbarNativeInterop.DestroyMenu(hMenu);
        }
    }

    private void HandleMenuCommand(int cmd, UiSettings currentSettings)
    {
        switch (cmd)
        {
            case CmdShowWindow:
                ShowMainWindow();
                break;
            case CmdOverlayToggle:
                if (Current.MainWindow is MainWindow mw1)
                    mw1.Dispatcher.Invoke(() => mw1.ToggleOverlay(!currentSettings.OverlayEnabled));
                break;
            case CmdTaskbarToggle:
                if (Current.MainWindow is MainWindow mw2)
                    mw2.Dispatcher.Invoke(() => mw2.ToggleTaskbarWidget(!currentSettings.TaskbarWidgetEnabled));
                break;
            case CmdExit:
                if (Current.MainWindow is MainWindow mw3)
                    mw3.Dispatcher.Invoke(() => BeginShutdown());
                else
                    BeginShutdown();
                break;
        }
    }

    private IntPtr GetMenuHwnd()
    {
        if (Current.MainWindow is Window mainWindow)
        {
            var handle = new WindowInteropHelper(mainWindow).Handle;
            if (handle != IntPtr.Zero) return handle;
        }
        return TaskbarNativeInterop.GetDesktopWindow();
    }

    private void ShowMainWindow()
    {
        if (Current.MainWindow is MainWindow mainWindow)
            mainWindow.Dispatcher.Invoke(() => mainWindow.ShowMainWindow());
    }

    private string GetText(string key) =>
        _texts.TryGetValue(key, out var value) ? value : key;
}
