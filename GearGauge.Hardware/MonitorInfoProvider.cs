using System.Runtime.InteropServices;
using System.Windows.Forms;
using GearGauge.Core.Contracts;
using GearGauge.Core.Models;

namespace GearGauge.Hardware;

public sealed class MonitorInfoProvider : IMonitorInfoProvider
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        return Screen.AllScreens
            .Select((screen, index) => new MonitorInfo
            {
                Index = index,
                DeviceName = screen.DeviceName,
                FriendlyName = screen.DeviceName,
                Width = screen.Bounds.Width,
                Height = screen.Bounds.Height,
                RefreshRate = GetRefreshRate(screen.DeviceName),
                IsPrimary = screen.Primary
            })
            .ToArray();
    }

    private static int GetRefreshRate(string deviceName)
    {
        var mode = new DevMode();
        mode.dmSize = (short)Marshal.SizeOf<DevMode>();

        return EnumDisplaySettings(deviceName, EnumCurrentSettings, ref mode)
            ? mode.dmDisplayFrequency
            : 0;
    }

    private const int EnumCurrentSettings = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(
        string? deviceName,
        int modeNum,
        ref DevMode devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
