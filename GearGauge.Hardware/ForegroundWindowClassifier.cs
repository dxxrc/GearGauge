using GearGauge.Core.Models;

namespace GearGauge.Hardware;

internal enum ForegroundContentKind
{
    Unknown,
    Game
}

internal static class ForegroundWindowClassifier
{
    private static readonly HashSet<string> VideoProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "vlc",
        "mpc-hc",
        "mpc-be",
        "potplayermini64",
        "potplayermini",
        "mpv",
        "wmplayer",
        "video.ui",
        "applicationframehost",
        "chrome",
        "msedge",
        "msedgewebview2",
        "firefox",
        "brave",
        "opera"
    };

    private static readonly HashSet<string> ExcludedGameProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer",
        "searchhost",
        "searchapp",
        "shellexperiencehost",
        "taskmgr",
        "cmd",
        "powershell",
        "windowsterminal",
        "devenv",
        "code",
        "rider64",
        "rider",
        "idea64",
        "pycharm64",
        "webstorm64",
        "clion64",
        "datagrip64",
        "goland64",
        "studio64",
        "typora",
        "notepad",
        "notepad++",
        "obsidian",
        "winword",
        "excel",
        "powerpnt",
        "outlook",
        "geargauge.ui",
        "dwm",
        "steam",
        "steamservice",
        "steamwebhelper",
        "gameoverlayui",
        "gameoverlayui64",
        "gamebarpresencewriter",
        "nvcontainer",
        "nvidia app",
        "radeonsoftware",
        "logioptionsplus",
        "logioptionsplus_agent",
        "logioptionsplus_updater",
        "lghub",
        "ghub",
        "razer synapse",
        "msiafterburner"
    };

    private static readonly string[] VideoTitleKeywords =
    [
        "youtube",
        "bilibili",
        "netflix",
        "twitch",
        "vimeo",
        "media player",
        "movie",
        "video"
    ];

    private static readonly string[] UtilityTitleKeywords =
    [
        "option",
        "setting",
        "preference",
        "control center",
        "manager",
        "dashboard",
        "studio",
        "assistant",
        "updater",
        "configuration",
        "config",
        "editor",
        "markdown",
        "document",
        "workspace",
        "solution"
    ];

    public static ForegroundContentKind Classify(ForegroundWindowInfo window, IReadOnlyList<MonitorInfo> monitors)
    {
        if (IsLikelyGame(window, monitors))
        {
            return ForegroundContentKind.Game;
        }

        return ForegroundContentKind.Unknown;
    }

    public static bool IsLikelyVideo(ForegroundWindowInfo window)
    {
        if (IsLikelyVideoProcess(window.ProcessName))
        {
            if (!IsBrowser(window.ProcessName))
            {
                return true;
            }

            return VideoTitleKeywords.Any(keyword =>
                window.WindowTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        return VideoTitleKeywords.Any(keyword =>
            window.WindowTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsLikelyGame(ForegroundWindowInfo window, IReadOnlyList<MonitorInfo> monitors)
    {
        if (IsExcludedGameProcess(window.ProcessName) || IsLikelyVideo(window))
        {
            return false;
        }

        if (IsBrowser(window.ProcessName))
        {
            return false;
        }

        if (UtilityTitleKeywords.Any(keyword =>
                window.WindowTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(window.WindowTitle))
        {
            return false;
        }

        var primaryMonitor = monitors.FirstOrDefault(static monitor => monitor.IsPrimary) ?? monitors.FirstOrDefault();
        if (primaryMonitor is null)
        {
            return true;
        }

        var widthRatio = primaryMonitor.Width <= 0 ? 0 : (double)window.Width / primaryMonitor.Width;
        var heightRatio = primaryMonitor.Height <= 0 ? 0 : (double)window.Height / primaryMonitor.Height;
        return widthRatio >= 0.6 && heightRatio >= 0.6;
    }

    public static bool IsLikelyVideoProcess(string processName)
    {
        return VideoProcesses.Contains(processName);
    }

    public static bool IsExcludedGameProcess(string processName)
    {
        return ExcludedGameProcesses.Contains(processName);
    }

    public static bool IsLikelyInteractiveGameProcess(string processName)
    {
        return !string.IsNullOrWhiteSpace(processName) &&
               !IsExcludedGameProcess(processName) &&
               !IsLikelyVideoProcess(processName) &&
               !IsBrowser(processName);
    }

    private static bool IsBrowser(string processName)
    {
        return processName.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("firefox", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("brave", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("opera", StringComparison.OrdinalIgnoreCase);
    }
}
