using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using GearGauge.Core.Models;

namespace GearGauge.Hardware;

internal static class HwInfoSharedMemoryReader
{
    private const uint HwInfoSignature = 0x4E695748; // "HWiN"

    private static readonly string[] MapNames = ["Global\\HWiNFO64", "Global\\HWiNFO32"];

    public static HwInfoSnapshot? TryReadSnapshot()
    {
        foreach (var mapName in MapNames)
        {
            try
            {
                using var mmf = MemoryMappedFile.OpenExisting(mapName);
                using var accessor = mmf.CreateViewAccessor();

                accessor.Read(0, out HwInfoHeader header);
                if (header.dwSignature != HwInfoSignature || header.wNumSensorElements == 0)
                {
                    continue;
                }

                return ParseSensors(accessor, header);
            }
            catch
            {
                continue;
            }
        }

        return null;
    }

    private static HwInfoSnapshot ParseSensors(MemoryMappedViewAccessor accessor, HwInfoHeader header)
    {
        OptionalFloat cpuTemp = OptionalFloat.None;
        OptionalFloat cpuPower = OptionalFloat.None;
        OptionalFloat cpuClock = OptionalFloat.None;

        var elementSize = header.dwSizeOfSensorElement > 0
            ? (int)header.dwSizeOfSensorElement
            : Marshal.SizeOf<HwInfoSensorElement>();

        var baseOffset = (int)header.dwHeaderSize;

        for (var i = 0; i < header.wNumSensorElements; i++)
        {
            var offset = baseOffset + i * elementSize;
            if (offset + elementSize > accessor.Capacity)
            {
                break;
            }

            accessor.Read(offset, out HwInfoSensorElement element);
            var name = NormalizeName(element.szSensorNameOrig);
            var unit = NormalizeName(element.szUnit);

            if (!float.IsFinite((float)element.dValue) || element.dValue <= 0)
            {
                continue;
            }

            var value = (float)element.dValue;

            if (IsCpuTemperature(name, unit))
            {
                cpuTemp = cpuTemp.HasValue ? Math.Max(cpuTemp.Value!.Value, value) : value;
            }
            else if (IsCpuPower(name, unit))
            {
                cpuPower = cpuPower.HasValue ? Math.Max(cpuPower.Value!.Value, value) : value;
            }
            else if (IsCpuClock(name, unit))
            {
                cpuClock = cpuClock.HasValue ? Math.Max(cpuClock.Value!.Value, value) : value;
            }
        }

        if (!cpuTemp.HasValue && !cpuPower.HasValue && !cpuClock.HasValue)
        {
            return null!;
        }

        return new HwInfoSnapshot(cpuTemp, cpuPower, cpuClock);
    }

    internal static bool IsCpuTemperature(string name, string unit)
    {
        return name.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
               (name.Contains("Temp", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Package", StringComparison.OrdinalIgnoreCase)) &&
               !name.Contains("GPU", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsCpuPower(string name, string unit)
    {
        return name.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
               (name.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("PPT", StringComparison.OrdinalIgnoreCase)) &&
               unit.Contains("W", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("GPU", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsCpuClock(string name, string unit)
    {
        return name.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
               (name.Contains("Clock", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Core", StringComparison.OrdinalIgnoreCase)) &&
               (unit.Contains("MHz", StringComparison.OrdinalIgnoreCase) ||
                unit.Contains("GHz", StringComparison.OrdinalIgnoreCase)) &&
               !name.Contains("GPU", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeName(byte[] rawBytes)
    {
        var span = rawBytes.AsSpan();
        var nullIndex = span.IndexOf((byte)0);
        if (nullIndex >= 0)
        {
            span = span[..nullIndex];
        }

        return Encoding.ASCII.GetString(span).Trim();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct HwInfoHeader
    {
        public uint dwSignature;
        public uint dwVersion;
        public uint dwRevision;
        public uint dwHeaderSize;
        public uint dwOffset;
        public ushort wNumSensorElements;
        public ushort wNumSensorElementGroups;
        public uint dwSizeOfSensorElement;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    private struct HwInfoSensorElement
    {
        public uint dwSensorInst;
        public uint dwSensorID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] szSensorNameOrig;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] szSensorNameUser;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] szUnit;
        public double dValue;
        public double dValueMin;
        public double dValueMax;
    }
}

internal sealed record HwInfoSnapshot(
    OptionalFloat CpuTemperatureCelsius,
    OptionalFloat CpuPowerWatt,
    OptionalFloat CpuClockGHz);
