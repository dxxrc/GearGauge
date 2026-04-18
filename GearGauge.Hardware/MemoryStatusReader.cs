using System.Runtime.InteropServices;
using GearGauge.Core.Models;

namespace GearGauge.Hardware;

public static class MemoryStatusReader
{
    public static MemoryMetrics Read(string modelName)
    {
        var status = new MemoryStatusEx();
        status.dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();

        if (!GlobalMemoryStatusEx(ref status))
        {
            return new MemoryMetrics
            {
                ModelName = modelName
            };
        }

        var totalGb = BytesToGb(status.ullTotalPhys);
        var availableGb = BytesToGb(status.ullAvailPhys);
        var usedGb = Math.Max(0, totalGb - availableGb);

        return new MemoryMetrics
        {
            ModelName = modelName,
            TotalGB = totalGb,
            AvailableGB = availableGb,
            UsedGB = usedGb,
            UsagePercent = totalGb <= 0 ? 0 : (usedGb / totalGb) * 100f
        };
    }

    private static float BytesToGb(ulong bytes) => (float)(bytes / 1024d / 1024d / 1024d);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
