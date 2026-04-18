using System.Windows.Media;
using GearGauge.UI.Settings;
using GearGauge.UI.ViewModels;
using Microsoft.Win32;

namespace GearGauge.UI.Themes;

public static class ThemePalette
{
    public static void Apply(ThemeViewModel theme, string themeMode)
    {
        var effectiveMode = ResolveEffectiveTheme(themeMode);
        var isDark = string.Equals(effectiveMode, ThemeModes.Dark, StringComparison.OrdinalIgnoreCase);

        theme.WindowBackgroundBrush = Create(isDark ? "#1F1F1F" : "#F5F7FB");
        theme.WindowChromeBrush = Create(isDark ? "#1C1C1C" : "#FBFBFD");
        theme.NavigationBackgroundBrush = Create(isDark ? "#1C1C1C" : "#FBFBFD");
        theme.PageBackgroundBrush = Create(isDark ? "#1C1C1C" : "#FFFFFF");
        theme.CardBackgroundBrush = Create(isDark ? "#2D2D2D" : "#FFFFFF");
        theme.SecondaryCardBackgroundBrush = Create(isDark ? "#383838" : "#F7F9FC");
        theme.BorderBrush = Create(isDark ? "#404040" : "#D6DCE8");
        theme.AccentBrush = Create("#0F6CBD");
        theme.AccentForegroundBrush = Create("#FFFFFF");
        theme.TextPrimaryBrush = Create(isDark ? "#F5F5F5" : "#111827");
        theme.TextSecondaryBrush = Create(isDark ? "#CCCCCC" : "#475569");
        theme.TextMutedBrush = Create(isDark ? "#999999" : "#64748B");
        theme.InputBackgroundBrush = Create(isDark ? "#2D2D2D" : "#F7F9FC");
        theme.InputBorderBrush = Create(isDark ? "#404040" : "#CBD5E1");
        theme.SelectedNavigationBackgroundBrush = Create(isDark ? "#3A3A3A" : "#DCEBFF");
        theme.HoverNavigationBackgroundBrush = Create(isDark ? "#333333" : "#EEF3FB");
        theme.SeparatorBrush = Create(isDark ? "#333333" : "#E5E7EB");
    }

    private static string ResolveEffectiveTheme(string themeMode)
    {
        if (!string.Equals(themeMode, ThemeModes.System, StringComparison.OrdinalIgnoreCase))
        {
            return themeMode;
        }

        try
        {
            using var personalizeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = personalizeKey?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 0 ? ThemeModes.Dark : ThemeModes.Light;
        }
        catch
        {
            return ThemeModes.Light;
        }
    }

    private static SolidColorBrush Create(string hex, bool freeze = true)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        if (freeze)
        {
            brush.Freeze();
        }

        return brush;
    }
}
