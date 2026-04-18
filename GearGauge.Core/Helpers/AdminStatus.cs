using System.Security.Principal;

namespace GearGauge.Core.Helpers;

public static class AdminStatus
{
    private static readonly Lazy<bool> _isElevated = new(() =>
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    });

    public static bool IsElevated => _isElevated.Value;
}
