namespace GearGauge.UI.Settings;

public static class SciFiPalettes
{
    public static Dictionary<string, string> Get(string paletteName) => paletteName switch
    {
        OverlayPaletteNames.NeonCyber => NeonCyber(),
        OverlayPaletteNames.QuantumIce => QuantumIce(),
        OverlayPaletteNames.SolarFire => SolarFire(),
        OverlayPaletteNames.MatrixGreen => MatrixGreen(),
        OverlayPaletteNames.PlasmaViolet => PlasmaViolet(),
        OverlayPaletteNames.Titanium => Titanium(),
        OverlayPaletteNames.EmeraldCircuit => EmeraldCircuit(),
        OverlayPaletteNames.PhantomRed => PhantomRed(),
        _ => NeonCyber()
    };

    private static Dictionary<string, string> NeonCyber() => new()
    {
        [OverlayMetricKeys.CpuUsage] = "#00FFCC",
        [OverlayMetricKeys.CpuTemp] = "#FF6B6B",
        [OverlayMetricKeys.CpuPower] = "#FFD93D",
        [OverlayMetricKeys.CpuClock] = "#6BCB77",
        [OverlayMetricKeys.GpuUsage] = "#00D2FF",
        [OverlayMetricKeys.GpuTemp] = "#FF8C00",
        [OverlayMetricKeys.GpuPower] = "#C084FC",
        [OverlayMetricKeys.GpuClock] = "#4ADE80",
        [OverlayMetricKeys.MemUsed] = "#FF69B4",
        [OverlayMetricKeys.MemUsage] = "#F472B6",
        [OverlayMetricKeys.FpsDisplay] = "#34D399",
        [OverlayMetricKeys.FpsGame] = "#A78BFA",
        [OverlayMetricKeys.NetDownload] = "#38BDF8",
        [OverlayMetricKeys.NetUpload] = "#FB923C"
    };

    private static Dictionary<string, string> QuantumIce() => new()
    {
        [OverlayMetricKeys.CpuUsage] = "#7DF9FF",
        [OverlayMetricKeys.CpuTemp] = "#B0E0E6",
        [OverlayMetricKeys.CpuPower] = "#E0FFFF",
        [OverlayMetricKeys.CpuClock] = "#87CEEB",
        [OverlayMetricKeys.GpuUsage] = "#ADD8E6",
        [OverlayMetricKeys.GpuTemp] = "#F0F8FF",
        [OverlayMetricKeys.GpuPower] = "#B0C4DE",
        [OverlayMetricKeys.GpuClock] = "#AFEEEE",
        [OverlayMetricKeys.MemUsed] = "#6495ED",
        [OverlayMetricKeys.MemUsage] = "#00BFFF",
        [OverlayMetricKeys.FpsDisplay] = "#87CEFA",
        [OverlayMetricKeys.FpsGame] = "#E6E6FA",
        [OverlayMetricKeys.NetDownload] = "#00CED1",
        [OverlayMetricKeys.NetUpload] = "#48D1CC"
    };

    private static Dictionary<string, string> SolarFire() => new()
    {
        [OverlayMetricKeys.CpuUsage] = "#FFBF00",
        [OverlayMetricKeys.CpuTemp] = "#FF6600",
        [OverlayMetricKeys.CpuPower] = "#FF4444",
        [OverlayMetricKeys.CpuClock] = "#FFD700",
        [OverlayMetricKeys.GpuUsage] = "#FFA500",
        [OverlayMetricKeys.GpuTemp] = "#FF4500",
        [OverlayMetricKeys.GpuPower] = "#FF8C00",
        [OverlayMetricKeys.GpuClock] = "#FFCC00",
        [OverlayMetricKeys.MemUsed] = "#FF6347",
        [OverlayMetricKeys.MemUsage] = "#FF7F50",
        [OverlayMetricKeys.FpsDisplay] = "#FFDAB9",
        [OverlayMetricKeys.FpsGame] = "#FFE4B5",
        [OverlayMetricKeys.NetDownload] = "#E9967A",
        [OverlayMetricKeys.NetUpload] = "#FA8072"
    };

    private static Dictionary<string, string> MatrixGreen() => new()
    {
        [OverlayMetricKeys.CpuUsage] = "#00FF41",
        [OverlayMetricKeys.CpuTemp] = "#33CC33",
        [OverlayMetricKeys.CpuPower] = "#00CC00",
        [OverlayMetricKeys.CpuClock] = "#66FF66",
        [OverlayMetricKeys.GpuUsage] = "#00FF66",
        [OverlayMetricKeys.GpuTemp] = "#33FF33",
        [OverlayMetricKeys.GpuPower] = "#00FF00",
        [OverlayMetricKeys.GpuClock] = "#00CC66",
        [OverlayMetricKeys.MemUsed] = "#009933",
        [OverlayMetricKeys.MemUsage] = "#66CC66",
        [OverlayMetricKeys.FpsDisplay] = "#33CC00",
        [OverlayMetricKeys.FpsGame] = "#00FF33",
        [OverlayMetricKeys.NetDownload] = "#00CC33",
        [OverlayMetricKeys.NetUpload] = "#009966"
    };

    private static Dictionary<string, string> PlasmaViolet() => new()
    {
        [OverlayMetricKeys.CpuUsage] = "#BF40BF",
        [OverlayMetricKeys.CpuTemp] = "#FF69B4",
        [OverlayMetricKeys.CpuPower] = "#E6E6FA",
        [OverlayMetricKeys.CpuClock] = "#DA70D6",
        [OverlayMetricKeys.GpuUsage] = "#FF00FF",
        [OverlayMetricKeys.GpuTemp] = "#9370DB",
        [OverlayMetricKeys.GpuPower] = "#BA55D3",
        [OverlayMetricKeys.GpuClock] = "#8A2BE2",
        [OverlayMetricKeys.MemUsed] = "#DDA0DD",
        [OverlayMetricKeys.MemUsage] = "#EE82EE",
        [OverlayMetricKeys.FpsDisplay] = "#D8BFD8",
        [OverlayMetricKeys.FpsGame] = "#9400D3",
        [OverlayMetricKeys.NetDownload] = "#9932CC",
        [OverlayMetricKeys.NetUpload] = "#800080"
    };

    private static Dictionary<string, string> Titanium() => new()
    {
        [OverlayMetricKeys.CpuUsage] = "#C0C0C0",
        [OverlayMetricKeys.CpuTemp] = "#D3D3D3",
        [OverlayMetricKeys.CpuPower] = "#F5F5F5",
        [OverlayMetricKeys.CpuClock] = "#A9A9A9",
        [OverlayMetricKeys.GpuUsage] = "#DCDCDC",
        [OverlayMetricKeys.GpuTemp] = "#E8E8E8",
        [OverlayMetricKeys.GpuPower] = "#B0B0B0",
        [OverlayMetricKeys.GpuClock] = "#C8C8C8",
        [OverlayMetricKeys.MemUsed] = "#D0D0D0",
        [OverlayMetricKeys.MemUsage] = "#E0E0E0",
        [OverlayMetricKeys.FpsDisplay] = "#F0F0F0",
        [OverlayMetricKeys.FpsGame] = "#A0A0A0",
        [OverlayMetricKeys.NetDownload] = "#B8B8B8",
        [OverlayMetricKeys.NetUpload] = "#CCCCCC"
    };

    private static Dictionary<string, string> EmeraldCircuit() => new()
    {
        [OverlayMetricKeys.CpuUsage] = "#50C878",
        [OverlayMetricKeys.CpuTemp] = "#3CB371",
        [OverlayMetricKeys.CpuPower] = "#2E8B57",
        [OverlayMetricKeys.CpuClock] = "#008080",
        [OverlayMetricKeys.GpuUsage] = "#20B2AA",
        [OverlayMetricKeys.GpuTemp] = "#5F9EA0",
        [OverlayMetricKeys.GpuPower] = "#66CDAA",
        [OverlayMetricKeys.GpuClock] = "#7FFFD4",
        [OverlayMetricKeys.MemUsed] = "#00FA9A",
        [OverlayMetricKeys.MemUsage] = "#00FF7F",
        [OverlayMetricKeys.FpsDisplay] = "#98FB98",
        [OverlayMetricKeys.FpsGame] = "#90EE90",
        [OverlayMetricKeys.NetDownload] = "#008B8B",
        [OverlayMetricKeys.NetUpload] = "#006400"
    };

    private static Dictionary<string, string> PhantomRed() => new()
    {
        [OverlayMetricKeys.CpuUsage] = "#DC143C",
        [OverlayMetricKeys.CpuTemp] = "#FF2400",
        [OverlayMetricKeys.CpuPower] = "#B22222",
        [OverlayMetricKeys.GpuUsage] = "#FF0000",
        [OverlayMetricKeys.GpuTemp] = "#FF4500",
        [OverlayMetricKeys.GpuPower] = "#FF6347",
        [OverlayMetricKeys.GpuClock] = "#CD5C5C",
        [OverlayMetricKeys.MemUsed] = "#F08080",
        [OverlayMetricKeys.MemUsage] = "#E9967A",
        [OverlayMetricKeys.FpsDisplay] = "#FA8072",
        [OverlayMetricKeys.FpsGame] = "#FFA07A",
        [OverlayMetricKeys.NetDownload] = "#FF7F50",
        [OverlayMetricKeys.NetUpload] = "#FF6B6B",
        [OverlayMetricKeys.CpuClock] = "#8B0000"
    };
}
