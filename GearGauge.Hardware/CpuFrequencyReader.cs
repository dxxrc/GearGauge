using System.Globalization;
using System.Runtime.InteropServices;
using GearGauge.Core.Models;

namespace GearGauge.Hardware;

internal sealed class CpuFrequencyReader : IDisposable
{
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;

    private readonly object _sync = new();
    private nint _queryHandle = IntPtr.Zero;
    private nint _counterHandle = IntPtr.Zero;
    private bool _initialized;
    private bool _failed;

    public CpuFrequencySnapshot ReadSnapshot()
    {
        nint dataBuffer = IntPtr.Zero;

        try
        {
            lock (_sync)
            {
                if (!EnsureInitialized())
                {
                    return CpuFrequencySnapshot.Empty;
                }

                if (PdhCollectQueryData(_queryHandle) != 0)
                {
                    return CpuFrequencySnapshot.Empty;
                }
            }

            uint bufferSize = 0;
            uint itemCount = 0;
            var result = PdhGetFormattedCounterArray(_counterHandle, PdhFmtDouble, ref bufferSize, ref itemCount, IntPtr.Zero);
            if (result != PdhMoreData || bufferSize == 0 || itemCount == 0)
            {
                return CpuFrequencySnapshot.Empty;
            }

            dataBuffer = Marshal.AllocHGlobal((int)bufferSize);
            if (PdhGetFormattedCounterArray(_counterHandle, PdhFmtDouble, ref bufferSize, ref itemCount, dataBuffer) != 0)
            {
                return CpuFrequencySnapshot.Empty;
            }

            var coreClocks = new Dictionary<int, OptionalFloat>();
            OptionalFloat packageClock = OptionalFloat.None;
            var itemSize = Marshal.SizeOf<PdhFormattedCounterValueItemDouble>();

            for (var i = 0; i < itemCount; i++)
            {
                var itemPointer = dataBuffer + (i * itemSize);
                var item = Marshal.PtrToStructure<PdhFormattedCounterValueItemDouble>(itemPointer);
                var instanceName = Marshal.PtrToStringUni(item.NamePointer);

                if (string.IsNullOrWhiteSpace(instanceName) || item.CounterValue.CStatus != 0)
                {
                    continue;
                }

                var frequencyGhz = (float)item.CounterValue.DoubleValue / 1000f;
                if (frequencyGhz <= 0 || !float.IsFinite(frequencyGhz))
                {
                    continue;
                }

                if (instanceName.Contains("_Total", StringComparison.OrdinalIgnoreCase))
                {
                    packageClock = frequencyGhz;
                    continue;
                }

                if (TryParseLogicalProcessorIndex(instanceName, out var index))
                {
                    coreClocks[index] = frequencyGhz;
                }
            }

            if (!packageClock.HasValue && coreClocks.Count > 0)
            {
                packageClock = coreClocks.Values.Average(static value => value.Value ?? 0f);
            }

            return new CpuFrequencySnapshot(packageClock, coreClocks);
        }
        catch
        {
            return CpuFrequencySnapshot.Empty;
        }
        finally
        {
            if (dataBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(dataBuffer);
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            CleanupHandles();
            _initialized = false;
        }
    }

    private bool EnsureInitialized()
    {
        if (_failed)
        {
            return false;
        }

        if (_initialized)
        {
            return true;
        }

        if (PdhOpenQuery(null, IntPtr.Zero, out _queryHandle) != 0)
        {
            _failed = true;
            CleanupHandles();
            return false;
        }

        if (PdhAddEnglishCounter(_queryHandle, @"\Processor Information(*)\Processor Frequency", IntPtr.Zero, out _counterHandle) != 0)
        {
            _failed = true;
            CleanupHandles();
            return false;
        }

        _initialized = true;
        return true;
    }

    private void CleanupHandles()
    {
        _counterHandle = IntPtr.Zero;

        if (_queryHandle != IntPtr.Zero)
        {
            PdhCloseQuery(_queryHandle);
            _queryHandle = IntPtr.Zero;
        }
    }

    private static bool TryParseLogicalProcessorIndex(string instanceName, out int index)
    {
        var parts = instanceName.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var group) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var processor))
        {
            index = (group * 64) + processor;
            return true;
        }

        if (parts.Length == 1 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out processor))
        {
            index = processor;
            return true;
        }

        index = -1;
        return false;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out nint query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(nint query, string fullCounterPath, IntPtr userData, out nint counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(nint query);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(nint query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArray(
        nint counter,
        uint format,
        ref uint bufferSize,
        ref uint itemCount,
        nint itemBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFormattedCounterValue
    {
        public uint CStatus;
        public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFormattedCounterValueItemDouble
    {
        public nint NamePointer;
        public PdhFormattedCounterValue CounterValue;
    }
}

internal sealed record CpuFrequencySnapshot(OptionalFloat PackageClockGHz, IReadOnlyDictionary<int, OptionalFloat> CoreClocksGHz)
{
    public static CpuFrequencySnapshot Empty { get; } =
        new(OptionalFloat.None, new Dictionary<int, OptionalFloat>());
}
