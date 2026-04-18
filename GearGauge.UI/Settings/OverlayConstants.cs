namespace GearGauge.UI.Settings;

public static class OverlayMetricKeys
{
    public const string CpuUsage = "CpuUsage";
    public const string CpuTemp = "CpuTemp";
    public const string CpuPower = "CpuPower";
    public const string CpuClock = "CpuClock";
    public const string GpuUsage = "GpuUsage";
    public const string GpuTemp = "GpuTemp";
    public const string GpuPower = "GpuPower";
    public const string GpuClock = "GpuClock";
    public const string MemUsed = "MemUsed";
    public const string MemUsage = "MemUsage";
    public const string FpsDisplay = "FpsDisplay";
    public const string FpsGame = "FpsGame";
    public const string NetDownload = "NetDownload";
    public const string NetUpload = "NetUpload";

    public static readonly string[] All =
    [
        CpuUsage, CpuTemp, CpuPower, CpuClock,
        GpuUsage, GpuTemp, GpuPower, GpuClock,
        MemUsed, MemUsage,
        FpsDisplay, FpsGame,
        NetDownload, NetUpload
    ];
}

public static class OverlayEdge
{
    public const string Top = "Top";
    public const string Bottom = "Bottom";
    public const string Left = "Left";
    public const string Right = "Right";

    public static readonly string[] All = [Top, Bottom, Left, Right];

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value, StringComparer.OrdinalIgnoreCase);
}

public static class OverlayAlignment
{
    public const string Start = "Start";
    public const string Center = "Center";
    public const string End = "End";

    public static readonly string[] All = [Start, Center, End];

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value, StringComparer.OrdinalIgnoreCase);
}

public static class OverlayDisplayMode
{
    public const string IconAndValue = "IconAndValue";
    public const string IconOnly = "IconOnly";
    public const string LabelOnly = "LabelOnly";
    public const string ValueOnly = "ValueOnly";

    public static readonly string[] All = [IconAndValue, IconOnly, LabelOnly, ValueOnly];

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value, StringComparer.OrdinalIgnoreCase);
}

public static class TaskbarPositionNames
{
    public const string LeftOfTray = "LeftOfTray";
    public const string RightOfStart = "RightOfStart";
    public const string Leftmost = "Leftmost";

    public static readonly string[] All = [LeftOfTray, RightOfStart, Leftmost];

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value, StringComparer.OrdinalIgnoreCase);
}

public static class OverlayPaletteNames
{
    public const string NeonCyber = "NeonCyber";
    public const string QuantumIce = "QuantumIce";
    public const string SolarFire = "SolarFire";
    public const string MatrixGreen = "MatrixGreen";
    public const string PlasmaViolet = "PlasmaViolet";
    public const string Titanium = "Titanium";
    public const string EmeraldCircuit = "EmeraldCircuit";
    public const string PhantomRed = "PhantomRed";

    public static readonly string[] All =
    [
        NeonCyber, QuantumIce, SolarFire, MatrixGreen,
        PlasmaViolet, Titanium, EmeraldCircuit, PhantomRed
    ];

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value, StringComparer.OrdinalIgnoreCase);
}
