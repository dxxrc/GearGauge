using System.Globalization;
using System.Runtime.InteropServices;

namespace GearGauge.Hardware;

internal sealed class GpuProcessActivityReader : IDisposable
{
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;

    private readonly object _sync = new();
    private nint _queryHandle = IntPtr.Zero;
    private nint _counterHandle = IntPtr.Zero;
    private bool _initialized;
    private bool _failed;

    public GpuProcessActivitySnapshot ReadSnapshot()
    {
        nint dataBuffer = IntPtr.Zero;

        try
        {
            lock (_sync)
            {
                if (!EnsureInitialized())
                {
                    return GpuProcessActivitySnapshot.Empty;
                }

                if (PdhCollectQueryData(_queryHandle) != 0)
                {
                    return GpuProcessActivitySnapshot.Empty;
                }
            }

            uint bufferSize = 0;
            uint itemCount = 0;
            var result = PdhGetFormattedCounterArray(_counterHandle, PdhFmtDouble, ref bufferSize, ref itemCount, IntPtr.Zero);
            if (result != PdhMoreData || bufferSize == 0 || itemCount == 0)
            {
                return GpuProcessActivitySnapshot.Empty;
            }

            dataBuffer = Marshal.AllocHGlobal((int)bufferSize);
            if (PdhGetFormattedCounterArray(_counterHandle, PdhFmtDouble, ref bufferSize, ref itemCount, dataBuffer) != 0)
            {
                return GpuProcessActivitySnapshot.Empty;
            }

            var processUtilization = new Dictionary<int, float>();
            var itemSize = Marshal.SizeOf<PdhFormattedCounterValueItemDouble>();

            for (var i = 0; i < itemCount; i++)
            {
                var itemPointer = dataBuffer + (i * itemSize);
                var item = Marshal.PtrToStructure<PdhFormattedCounterValueItemDouble>(itemPointer);
                var instanceName = Marshal.PtrToStringUni(item.NamePointer);

                if (string.IsNullOrWhiteSpace(instanceName) ||
                    item.CounterValue.CStatus != 0 ||
                    !TryParseInstanceName(instanceName, out var processId, out var engineType) ||
                    !engineType.Equals("3D", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var utilization = (float)item.CounterValue.DoubleValue;
                if (utilization <= 0 || !float.IsFinite(utilization))
                {
                    continue;
                }

                processUtilization[processId] = processUtilization.TryGetValue(processId, out var existing)
                    ? existing + utilization
                    : utilization;
            }

            return new GpuProcessActivitySnapshot(processUtilization);
        }
        catch
        {
            return GpuProcessActivitySnapshot.Empty;
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
            _counterHandle = IntPtr.Zero;

            if (_queryHandle != IntPtr.Zero)
            {
                PdhCloseQuery(_queryHandle);
                _queryHandle = IntPtr.Zero;
            }

            _initialized = false;
        }
    }

    internal static bool TryParseInstanceName(string instanceName, out int processId, out string engineType)
    {
        processId = 0;
        engineType = string.Empty;

        var parts = instanceName.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("pid", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(parts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedProcessId) &&
                parsedProcessId > 0)
            {
                processId = parsedProcessId;
            }

            if (parts[i].Equals("engtype", StringComparison.OrdinalIgnoreCase))
            {
                engineType = parts[i + 1];
            }
        }

        return processId > 0 && !string.IsNullOrWhiteSpace(engineType);
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
            return false;
        }

        if (PdhAddEnglishCounter(_queryHandle, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _counterHandle) != 0)
        {
            _failed = true;
            return false;
        }

        _initialized = true;
        return true;
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

internal sealed record GpuProcessActivitySnapshot(IReadOnlyDictionary<int, float> ProcessUtilizationPercent)
{
    public static GpuProcessActivitySnapshot Empty { get; } =
        new(new Dictionary<int, float>());
}
