using System.Runtime.InteropServices;

namespace GearGauge.Hardware;

internal sealed class DxgiFrameCounter : IDisposable
{
    private readonly Dictionary<int, OutputCounter> _outputs = new();
    private Task? _countingTask;
    private CancellationTokenSource? _cts;
    private volatile bool _isRunning;
    private string _initStatus = "NotInitialized";

    public bool IsAvailable => _outputs.Count > 0;
    public float CurrentFps => _outputs.Values.FirstOrDefault()?.CurrentFps ?? 0;
    public string InitStatus => _initStatus;

    public float GetOutputFps(int outputIndex)
    {
        return _outputs.TryGetValue(outputIndex, out var counter) ? counter.CurrentFps : 0;
    }

    public bool Initialize()
    {
        try
        {
            var factory1Iid = new Guid(0x770AAE78, 0xF26F, 0x4DBA, 0xA8, 0x29, 0x25, 0x3C, 0x83, 0xD1, 0xB3, 0x87);
            var hr = CreateDXGIFactory1(ref factory1Iid, out var factory);
            if (hr < 0 || factory == IntPtr.Zero)
            {
                _initStatus = $"CreateDXGIFactory1 failed: 0x{hr:X8}";
                return false;
            }

            try
            {
                var enumAdapters1 = VtableFn<EnumAdapters1Fn>(factory, 12);
                var output1Iid = new Guid(0x00CDDEA8, 0x939B, 0x4B83, 0xA3, 0x40, 0xA6, 0x85, 0x22, 0x66, 0x66, 0xCC);
                var globalOutputIndex = 0;

                for (uint adapterIndex = 0; adapterIndex < 16; adapterIndex++)
                {
                    hr = enumAdapters1(factory, adapterIndex, out var adapter);
                    if (hr < 0 || adapter == IntPtr.Zero)
                    {
                        break;
                    }

                    try
                    {
                        var enumOutputs = VtableFn<EnumOutputsFn>(adapter, 7);
                        for (uint outputIndex = 0; outputIndex < 16; outputIndex++)
                        {
                            hr = enumOutputs(adapter, outputIndex, out var output);
                            if (hr < 0 || output == IntPtr.Zero)
                            {
                                break;
                            }

                            try
                            {
                                if (Marshal.QueryInterface(output, ref output1Iid, out var output1) != 0)
                                {
                                    continue;
                                }

                                try
                                {
                                    hr = D3D11CreateDevice(
                                        adapter,
                                        D3D_DRIVER_TYPE_UNKNOWN,
                                        IntPtr.Zero,
                                        0,
                                        IntPtr.Zero,
                                        0,
                                        D3D11_SDK_VERSION,
                                        out var device,
                                        out _,
                                        out _);
                                    if (hr < 0 || device == IntPtr.Zero)
                                    {
                                        continue;
                                    }

                                    var duplicateOutput = VtableFn<DuplicateOutputFn>(output1, 20);
                                    hr = duplicateOutput(output1, device, out var duplication);
                                    if (hr >= 0 && duplication != IntPtr.Zero)
                                    {
                                        _outputs[globalOutputIndex++] = new OutputCounter
                                        {
                                            Device = device,
                                            Duplication = duplication
                                        };
                                    }
                                    else
                                    {
                                        Marshal.Release(device);
                                    }
                                }
                                finally
                                {
                                    Marshal.Release(output1);
                                }
                            }
                            finally
                            {
                                Marshal.Release(output);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.Release(adapter);
                    }
                }
            }
            finally
            {
                Marshal.Release(factory);
            }

            _initStatus = _outputs.Count > 0
                ? $"OK({_outputs.Count} outputs)"
                : "No outputs duplicated";

            return _outputs.Count > 0;
        }
        catch (Exception ex)
        {
            _initStatus = $"Exception: {ex.Message}";
            return false;
        }
    }

    public void Start()
    {
        if (_outputs.Count == 0 || _isRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _isRunning = true;
        _countingTask = Task.Run(() => CountFrames(_cts.Token), _cts.Token);
    }

    private void CountFrames(CancellationToken ct)
    {
        foreach (var counter in _outputs.Values)
        {
            counter.Acquire = VtableFn<AcquireNextFrameFn>(counter.Duplication, 9);
            counter.Release = VtableFn<ReleaseFrameFn>(counter.Duplication, 15);
            counter.WindowStart = DateTimeOffset.UtcNow;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                foreach (var counter in _outputs.Values)
                {
                    var hr = counter.Acquire!(counter.Duplication, 20, out var frameInfo, out var resource);
                    if (hr == 0)
                    {
                        counter.TotalFrames += Math.Max(1, frameInfo.AccumulatedFrames);
                        counter.Release!(counter.Duplication);
                        if (resource != IntPtr.Zero)
                        {
                            Marshal.Release(resource);
                        }
                    }

                    var now = DateTimeOffset.UtcNow;
                    var elapsed = (now - counter.WindowStart).TotalSeconds;
                    if (elapsed >= 1.0)
                    {
                        counter.CurrentFps = (float)(counter.TotalFrames / elapsed);
                        counter.TotalFrames = 0;
                        counter.WindowStart = now;
                    }
                }
            }
            catch
            {
                Thread.Sleep(100);
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try
        {
            _countingTask?.Wait(2000);
        }
        catch
        {
        }

        _cts?.Dispose();

        foreach (var counter in _outputs.Values)
        {
            if (counter.Duplication != IntPtr.Zero)
            {
                Marshal.Release(counter.Duplication);
            }

            if (counter.Device != IntPtr.Zero)
            {
                Marshal.Release(counter.Device);
            }
        }

        _outputs.Clear();
    }

    private static T VtableFn<T>(IntPtr comObject, int index) where T : Delegate
    {
        var vtable = Marshal.ReadIntPtr(comObject);
        var fnPtr = Marshal.ReadIntPtr(vtable, index * IntPtr.Size);
        return (T)Marshal.GetDelegateForFunctionPointer(fnPtr, typeof(T));
    }

    private sealed class OutputCounter
    {
        public IntPtr Device;
        public IntPtr Duplication;
        public AcquireNextFrameFn? Acquire;
        public ReleaseFrameFn? Release;
        public float CurrentFps;
        public int TotalFrames;
        public DateTimeOffset WindowStart;
    }

    private const int D3D_DRIVER_TYPE_UNKNOWN = 0;
    private const int D3D11_SDK_VERSION = 7;

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr factory);

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        IntPtr adapter,
        int driverType,
        IntPtr software,
        int flags,
        IntPtr featureLevels,
        int featureLevelsCount,
        int sdkVersion,
        out IntPtr device,
        out int featureLevel,
        out IntPtr immediateContext);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1Fn(IntPtr self, uint index, out IntPtr adapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumOutputsFn(IntPtr self, uint index, out IntPtr output);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DuplicateOutputFn(IntPtr self, IntPtr device, out IntPtr duplication);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AcquireNextFrameFn(IntPtr self, int timeoutMs, out DxgiOutduplFrameInfo info, out IntPtr resource);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReleaseFrameFn(IntPtr self);

    [StructLayout(LayoutKind.Sequential)]
    private struct DxgiOutduplFrameInfo
    {
        public long LastPresentTime;
        public long LastMouseUpdateTime;
        public int AccumulatedFrames;
        public int RectsCoalesced;
        public int ProtectedContentMask;
    }
}
