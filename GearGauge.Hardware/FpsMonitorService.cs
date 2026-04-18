using GearGauge.Core.Contracts;
using GearGauge.Core.Helpers;
using GearGauge.Core.Models;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System.Diagnostics;
using System.Windows.Forms;

namespace GearGauge.Hardware;

public sealed class FpsMonitorService : IFpsMonitorService
{
    private static readonly Guid DxgiProviderGuid = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private static readonly Guid DxgKrnlProviderGuid = new("802EC45A-1E99-4B83-9920-87C98277BA9D");
    private static readonly Guid D3d9ProviderGuid = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");

    private static readonly HashSet<string> KernelPresentTasks = new(StringComparer.OrdinalIgnoreCase)
    {
        "PresentHistoryDetailed",
        "PresentHistory",
        "Present"
    };

    private readonly object _sync = new();
    private TraceEventSession? _session;
    private Task? _processingTask;
    private readonly Dictionary<int, Queue<long>> _processApiPresentTimestamps = new();
    private readonly Dictionary<int, TimedFpsSample> _latestProcessApiFps = new();
    private readonly Dictionary<int, Queue<long>> _processKernelPresentTimestamps = new();
    private readonly Dictionary<int, TimedFpsSample> _latestProcessKernelFps = new();
    private readonly Dictionary<int, string> _processNames = new();
    private readonly Dictionary<nint, DwmFrameSample> _dwmFrameSamples = new();
    private readonly Dictionary<nint, TimedFpsSample> _latestWindowFps = new();
    private readonly GpuProcessActivityReader _gpuProcessActivityReader = new();
    private string _providerStatus = "NotInitialized";

    private DxgiFrameCounter? _dxgiCounter;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _dxgiCounter = new DxgiFrameCounter();
        if (_dxgiCounter.Initialize())
        {
            _dxgiCounter.Start();
        }

        try
        {
            lock (_sync)
            {
                if (_session is not null)
                {
                    return Task.CompletedTask;
                }

                _session = new TraceEventSession($"GearGauge-FpsSession-{Environment.ProcessId}")
                {
                    StopOnDispose = true
                };

                _session.EnableProvider(DxgiProviderGuid);
                _session.EnableProvider(DxgKrnlProviderGuid);
                _session.EnableProvider(D3d9ProviderGuid);
                _session.Source.Dynamic.All += OnDynamicEvent;
                _processingTask = Task.Run(() => _session.Source.Process(), cancellationToken);
                _providerStatus = "ETW";
            }
        }
        catch (Exception ex)
        {
            _providerStatus = AdminStatus.IsElevated
                ? $"ETW failed: {ex.GetType().Name}"
                : "Non-admin";
        }

        return Task.CompletedTask;
    }

    public Task<FpsMetrics> CaptureAsync(IReadOnlyList<MonitorInfo> monitors, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetMonitor = monitors.FirstOrDefault(static m => m.IsPrimary) ?? monitors.FirstOrDefault();
        var foregroundWindow = ForegroundWindowReader.TryGet();
        var gpuActivitySnapshot = _gpuProcessActivityReader.ReadSnapshot();
        OptionalFloat gameFps = OptionalFloat.None;
        string? activeContent = null;
        var statusParts = new List<string>();

        OptionalFloat displayFps = OptionalFloat.None;
        if (_dxgiCounter is { IsAvailable: true })
        {
            var primaryOutputIndex = GetPrimaryOutputIndex(monitors);
            var fps = _dxgiCounter.GetOutputFps(primaryOutputIndex);
            if (fps <= 0)
            {
                fps = _dxgiCounter.CurrentFps;
            }

            if (fps > 0)
            {
                displayFps = fps;
            }

            statusParts.Add($"Display=DXGI-Dup({_dxgiCounter.InitStatus})");
        }
        else
        {
            var counterStatus = _dxgiCounter?.InitStatus ?? "not created";
            statusParts.Add($"Display=RefreshFallback({counterStatus})");
            displayFps = targetMonitor?.RefreshRate > 0 ? targetMonitor.RefreshRate : OptionalFloat.None;
        }

        lock (_sync)
        {
            if (_session is not null)
            {
                statusParts.Add("Game=ETW");
            }
            else if (!string.IsNullOrWhiteSpace(_providerStatus) &&
                     !string.Equals(_providerStatus, "NotInitialized", StringComparison.OrdinalIgnoreCase))
            {
                statusParts.Add($"Game={_providerStatus}");
            }
        }

        var foregroundContent = TryBuildForegroundContentCandidate(foregroundWindow, monitors, gpuActivitySnapshot);
        var trackedContent = foregroundContent;
        if (trackedContent is null || !trackedContent.Value.Fps.HasValue)
        {
            var detectedGame = TryGetPresentedGameCandidate(monitors);
            if (detectedGame is not null && detectedGame.Value.Fps.HasValue)
            {
                trackedContent = detectedGame;
            }
        }

        PresentedContentCandidate? gpuDetectedGame = null;
        if (trackedContent is null || !trackedContent.Value.Fps.HasValue)
        {
            gpuDetectedGame = TryGetGpuDetectedGameCandidate(monitors, gpuActivitySnapshot, requireFps: false);
            if (gpuDetectedGame is not null && gpuDetectedGame.Value.Fps.HasValue)
            {
                trackedContent = gpuDetectedGame;
            }
        }

        if (trackedContent is not null)
        {
            activeContent = trackedContent.Value.Label;

            if (trackedContent.Value.Kind == ForegroundContentKind.Game)
            {
                gameFps = trackedContent.Value.Fps;
            }

            if (trackedContent.Value.Kind != ForegroundContentKind.Unknown)
            {
                statusParts.Add($"{trackedContent.Value.Kind}={trackedContent.Value.ProcessName}({trackedContent.Value.Source})");
            }
        }
        else if (gpuDetectedGame is not null)
        {
            activeContent = gpuDetectedGame.Value.Label;
            statusParts.Add($"Game={gpuDetectedGame.Value.ProcessName}({gpuDetectedGame.Value.Source})");
            if (!AdminStatus.IsElevated)
            {
                statusParts.Add("Hint=RunAsAdmin");
            }
        }
        else if (foregroundWindow is not null)
        {
            activeContent = foregroundWindow.ProcessName;
        }

        return Task.FromResult(new FpsMetrics
        {
            DisplayOutputFps = displayFps,
            GameFps = gameFps,
            TargetDisplayIndex = targetMonitor?.Index,
            TargetDisplayName = targetMonitor?.FriendlyName,
            ActiveContent = activeContent,
            ProviderStatus = ComposeProviderStatus(statusParts)
        });
    }

    internal static OptionalFloat SelectBestContentFps(OptionalFloat processFps, OptionalFloat dwmFps)
    {
        if (processFps.HasValue)
        {
            return processFps;
        }

        if (dwmFps.HasValue)
        {
            return dwmFps;
        }

        return OptionalFloat.None;
    }

    internal static string ComposeProviderStatus(IEnumerable<string> statusParts)
    {
        return string.Join(" | ", statusParts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private OptionalFloat TryGetWindowFps(nint windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return OptionalFloat.None;
        }

        var now = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            DwmFrameSample? previousSample = _dwmFrameSamples.TryGetValue(windowHandle, out var existingSample)
                ? existingSample
                : null;
            var fps = DwmTimingReader.TryReadFramesPerSecond(windowHandle, ref previousSample);
            if (previousSample is { } updatedSample)
            {
                _dwmFrameSamples[windowHandle] = updatedSample;
            }

            if (fps.HasValue)
            {
                _latestWindowFps[windowHandle] = new TimedFpsSample(fps.Value!.Value, now);
            }

            CleanupStaleWindowSamples(now, windowHandle);

            if (_latestWindowFps.TryGetValue(windowHandle, out var latestSample) &&
                now - latestSample.TimestampUtc <= TimeSpan.FromSeconds(3))
            {
                return latestSample.Fps;
            }
        }

        return OptionalFloat.None;
    }

    private static int GetPrimaryOutputIndex(IReadOnlyList<MonitorInfo> monitors)
    {
        for (var i = 0; i < monitors.Count; i++)
        {
            if (monitors[i].IsPrimary)
            {
                var displayNumber = ExtractDisplayNumber(monitors[i].DeviceName);
                return displayNumber > 0 ? displayNumber - 1 : i;
            }
        }

        return 0;
    }

    private static int ExtractDisplayNumber(string deviceName)
    {
        var lastDigit = deviceName.AsSpan();
        var i = lastDigit.Length - 1;
        while (i >= 0 && char.IsDigit(lastDigit[i]))
        {
            i--;
        }

        if (i < lastDigit.Length - 1 && int.TryParse(lastDigit[(i + 1)..], out var num))
        {
            return num;
        }

        return 0;
    }

    public async ValueTask DisposeAsync()
    {
        _dxgiCounter?.Dispose();
        _dxgiCounter = null;
        _gpuProcessActivityReader.Dispose();

        TraceEventSession? session;
        Task? processingTask;

        lock (_sync)
        {
            session = _session;
            processingTask = _processingTask;
            _session = null;
            _processingTask = null;
            _processApiPresentTimestamps.Clear();
            _latestProcessApiFps.Clear();
            _processKernelPresentTimestamps.Clear();
            _latestProcessKernelFps.Clear();
            _processNames.Clear();
            _dwmFrameSamples.Clear();
            _latestWindowFps.Clear();
        }

        session?.Dispose();

        if (processingTask is not null)
        {
            try
            {
                await processingTask;
            }
            catch
            {
                // Ignore ETW shutdown failures.
            }
        }
    }

    private void OnDynamicEvent(TraceEvent traceEvent)
    {
        var processId = GetEffectiveProcessId(traceEvent);
        if (processId <= 0)
        {
            return;
        }

        var providerGuid = traceEvent.ProviderGuid;
        var taskName = traceEvent.TaskName;
        var opcodeName = traceEvent.OpcodeName;
        var nowTicks = DateTimeOffset.UtcNow.Ticks;

        lock (_sync)
        {
            if (providerGuid == DxgiProviderGuid || providerGuid == D3d9ProviderGuid)
            {
                if (!string.Equals(taskName, "Present", StringComparison.OrdinalIgnoreCase) ||
                    !IsAppPresentOpcode(opcodeName))
                {
                    return;
                }

                RecordFrameTimestamp(processId, nowTicks, _processApiPresentTimestamps, _latestProcessApiFps);
                return;
            }

            if (providerGuid == DxgKrnlProviderGuid)
            {
                if (string.IsNullOrWhiteSpace(taskName) ||
                    !KernelPresentTasks.Contains(taskName) ||
                    !IsKernelPresentOpcode(opcodeName))
                {
                    return;
                }

                RecordFrameTimestamp(processId, nowTicks, _processKernelPresentTimestamps, _latestProcessKernelFps);
            }
        }
    }

    private PresentedContentCandidate? TryBuildForegroundContentCandidate(
        ForegroundWindowInfo? foregroundWindow,
        IReadOnlyList<MonitorInfo> monitors,
        GpuProcessActivitySnapshot gpuActivitySnapshot)
    {
        if (foregroundWindow is null)
        {
            return null;
        }

        var classifiedKind = ForegroundWindowClassifier.Classify(foregroundWindow, monitors);
        var processFps = TryGetProcessFps(foregroundWindow.ProcessId);
        var dwmFps = TryGetWindowFps(foregroundWindow.Handle);
        var selectedContentFps = SelectBestContentFps(processFps, dwmFps);
        var gpuUtilization = GetProcessGpuUtilization(gpuActivitySnapshot, foregroundWindow.ProcessId);
        var source = GetProcessFpsSource(foregroundWindow.ProcessId);
        if (string.IsNullOrWhiteSpace(source))
        {
            source = dwmFps.HasValue ? "DWM" : "None";
        }

        if (classifiedKind == ForegroundContentKind.Unknown)
        {
            return null;
        }

        if (!IsMeaningfulForegroundGameCandidate(processFps, dwmFps, gpuUtilization))
        {
            return null;
        }

        return new PresentedContentCandidate(
            foregroundWindow.ProcessId,
            foregroundWindow.ProcessName,
            $"{classifiedKind}: {foregroundWindow.ProcessName}",
            selectedContentFps,
            source,
            classifiedKind);
    }

    private PresentedContentCandidate? TryGetPresentedGameCandidate(IReadOnlyList<MonitorInfo> monitors)
    {
        foreach (var sample in GetRecentPresentedProcessSamples())
        {
            var processName = ResolveProcessName(sample.ProcessId);
            if (!ForegroundWindowClassifier.IsLikelyInteractiveGameProcess(processName))
            {
                continue;
            }

            var window = ForegroundWindowReader.TryGetByProcessId(sample.ProcessId);
            if (window is not null)
            {
                var kind = ForegroundWindowClassifier.Classify(window, monitors);
                return new PresentedContentCandidate(
                    sample.ProcessId,
                    processName,
                    $"Game: {processName}",
                    sample.Fps,
                    sample.Source,
                    kind == ForegroundContentKind.Unknown ? ForegroundContentKind.Game : kind);
            }

            if (sample.Fps.HasValue && sample.Fps.Value >= 15f)
            {
                return new PresentedContentCandidate(
                    sample.ProcessId,
                    processName,
                    $"Game: {processName}",
                    sample.Fps,
                    sample.Source,
                    ForegroundContentKind.Game);
            }
        }

        return null;
    }

    private OptionalFloat TryGetProcessFps(int processId)
    {
        lock (_sync)
        {
            if (_latestProcessApiFps.TryGetValue(processId, out var sample) &&
                DateTimeOffset.UtcNow - sample.TimestampUtc <= TimeSpan.FromSeconds(3))
            {
                return sample.Fps;
            }

            if (_latestProcessKernelFps.TryGetValue(processId, out sample) &&
                DateTimeOffset.UtcNow - sample.TimestampUtc <= TimeSpan.FromSeconds(3))
            {
                return sample.Fps;
            }
        }

        return OptionalFloat.None;
    }

    private string? GetProcessFpsSource(int processId)
    {
        lock (_sync)
        {
            if (_latestProcessApiFps.TryGetValue(processId, out var sample) &&
                DateTimeOffset.UtcNow - sample.TimestampUtc <= TimeSpan.FromSeconds(3))
            {
                return "ETW-App";
            }

            if (_latestProcessKernelFps.TryGetValue(processId, out sample) &&
                DateTimeOffset.UtcNow - sample.TimestampUtc <= TimeSpan.FromSeconds(3))
            {
                return "ETW-Kernel";
            }
        }

        return null;
    }

    internal static bool IsMeaningfulForegroundGameCandidate(OptionalFloat processFps, OptionalFloat dwmFps, float gpuUtilizationPercent)
    {
        if (processFps.HasValue)
        {
            return true;
        }

        if (gpuUtilizationPercent >= 3f)
        {
            return true;
        }

        return false;
    }

    private PresentedContentCandidate? TryGetGpuDetectedGameCandidate(
        IReadOnlyList<MonitorInfo> monitors,
        GpuProcessActivitySnapshot gpuActivitySnapshot,
        bool requireFps)
    {
        foreach (var processActivity in gpuActivitySnapshot.ProcessUtilizationPercent.OrderByDescending(static kvp => kvp.Value))
        {
            if (processActivity.Value < 3f)
            {
                break;
            }

            var processName = ResolveProcessName(processActivity.Key);
            if (!ForegroundWindowClassifier.IsLikelyInteractiveGameProcess(processName))
            {
                continue;
            }

            var window = ForegroundWindowReader.TryGetByProcessId(processActivity.Key);
            if (window is null)
            {
                continue;
            }

            var kind = ForegroundWindowClassifier.Classify(window, monitors);
            var fps = TryGetWindowFps(window.Handle);
            var source = $"GPU({processActivity.Value:0.#}%)";
            if (!fps.HasValue)
            {
                fps = TryGetDisplayOutputFps(window.Handle);
                if (fps.HasValue)
                {
                    source = $"GPU+DXGI({processActivity.Value:0.#}%)";
                }
            }
            else
            {
                source = $"GPU+DWM({processActivity.Value:0.#}%)";
            }

            if (requireFps && !fps.HasValue)
            {
                continue;
            }

            return new PresentedContentCandidate(
                processActivity.Key,
                processName,
                $"Game: {processName}",
                fps,
                source,
                kind == ForegroundContentKind.Unknown ? ForegroundContentKind.Game : kind);
        }

        return null;
    }

    private OptionalFloat TryGetDisplayOutputFps(nint windowHandle)
    {
        if (_dxgiCounter is not { IsAvailable: true } || windowHandle == IntPtr.Zero)
        {
            return OptionalFloat.None;
        }

        try
        {
            var screen = Screen.FromHandle((IntPtr)windowHandle);
            var outputIndex = ExtractDisplayNumber(screen.DeviceName);
            if (outputIndex <= 0)
            {
                return _dxgiCounter.CurrentFps > 0 ? _dxgiCounter.CurrentFps : OptionalFloat.None;
            }

            var fps = _dxgiCounter.GetOutputFps(outputIndex - 1);
            return fps > 0 ? fps : OptionalFloat.None;
        }
        catch
        {
            return _dxgiCounter.CurrentFps > 0 ? _dxgiCounter.CurrentFps : OptionalFloat.None;
        }
    }

    private static float GetProcessGpuUtilization(GpuProcessActivitySnapshot snapshot, int processId)
    {
        return snapshot.ProcessUtilizationPercent.TryGetValue(processId, out var value) ? value : 0f;
    }

    private IReadOnlyList<PresentedProcessSample> GetRecentPresentedProcessSamples()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new Dictionary<int, PresentedProcessSample>();

        lock (_sync)
        {
            foreach (var kvp in _latestProcessApiFps)
            {
                if (now - kvp.Value.TimestampUtc <= TimeSpan.FromSeconds(3))
                {
                    samples[kvp.Key] = new PresentedProcessSample(kvp.Key, kvp.Value.Fps, "ETW-App");
                }
            }

            foreach (var kvp in _latestProcessKernelFps)
            {
                if (now - kvp.Value.TimestampUtc > TimeSpan.FromSeconds(3))
                {
                    continue;
                }

                if (!samples.TryGetValue(kvp.Key, out var existing) || kvp.Value.Fps > (existing.Fps.Value ?? 0f))
                {
                    samples[kvp.Key] = new PresentedProcessSample(kvp.Key, kvp.Value.Fps, "ETW-Kernel");
                }
            }
        }

        return samples.Values
            .OrderByDescending(static sample => sample.Fps.Value ?? 0f)
            .ToArray();
    }

    private static bool IsAppPresentOpcode(string? opcodeName)
    {
        return string.IsNullOrWhiteSpace(opcodeName) ||
               string.Equals(opcodeName, "Start", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(opcodeName, "Info", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKernelPresentOpcode(string? opcodeName)
    {
        return string.IsNullOrWhiteSpace(opcodeName) ||
               string.Equals(opcodeName, "Info", StringComparison.OrdinalIgnoreCase);
    }

    private void RecordFrameTimestamp(
        int processId,
        long nowTicks,
        IDictionary<int, Queue<long>> timestampStore,
        IDictionary<int, TimedFpsSample> latestStore)
    {
        if (!timestampStore.TryGetValue(processId, out var timestamps))
        {
            timestamps = new Queue<long>(64);
            timestampStore[processId] = timestamps;
        }

        timestamps.Enqueue(nowTicks);

        var cutoffTicks = nowTicks - TimeSpan.FromSeconds(2).Ticks;
        while (timestamps.Count > 0 && timestamps.Peek() < cutoffTicks)
        {
            timestamps.Dequeue();
        }

        if (timestamps.Count >= 2)
        {
            var elapsed = TimeSpan.FromTicks(nowTicks - timestamps.Peek()).TotalSeconds;
            if (elapsed > 0.05)
            {
                var fps = (float)((timestamps.Count - 1) / elapsed);
                latestStore[processId] = new TimedFpsSample(fps, new DateTimeOffset(nowTicks, TimeSpan.Zero));
            }
        }

        if (timestampStore.Count > 200)
        {
            CleanupStaleProcessSamples(nowTicks, timestampStore, latestStore);
        }
    }

    private int GetEffectiveProcessId(TraceEvent traceEvent)
    {
        foreach (var payloadName in traceEvent.PayloadNames)
        {
            if (!payloadName.Contains("Process", StringComparison.OrdinalIgnoreCase) &&
                !payloadName.Equals("PID", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = traceEvent.PayloadByName(payloadName);
            if (TryConvertToProcessId(value, out var payloadProcessId))
            {
                return payloadProcessId;
            }
        }

        return traceEvent.ProcessID;
    }

    private static bool TryConvertToProcessId(object? value, out int processId)
    {
        switch (value)
        {
            case int intValue when intValue > 0:
                processId = intValue;
                return true;
            case uint uintValue when uintValue > 0 && uintValue <= int.MaxValue:
                processId = (int)uintValue;
                return true;
            case long longValue when longValue > 0 && longValue <= int.MaxValue:
                processId = (int)longValue;
                return true;
            case ulong ulongValue when ulongValue > 0 && ulongValue <= int.MaxValue:
                processId = (int)ulongValue;
                return true;
            case string stringValue when int.TryParse(stringValue, out var parsedValue) && parsedValue > 0:
                processId = parsedValue;
                return true;
            default:
                processId = 0;
                return false;
        }
    }

    private string ResolveProcessName(int processId)
    {
        lock (_sync)
        {
            if (_processNames.TryGetValue(processId, out var cachedName) &&
                !string.IsNullOrWhiteSpace(cachedName))
            {
                return cachedName;
            }
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            var processName = process.ProcessName;

            lock (_sync)
            {
                _processNames[processId] = processName;
            }

            return processName;
        }
        catch
        {
            return processId.ToString();
        }
    }

    private static void CleanupStaleProcessSamples(
        long nowTicks,
        IDictionary<int, Queue<long>> timestampStore,
        IDictionary<int, TimedFpsSample> latestStore)
    {
        var staleKeys = new List<int>();
        foreach (var kvp in timestampStore)
        {
            if (kvp.Value.Count == 0 || TimeSpan.FromTicks(nowTicks - kvp.Value.Last()).TotalSeconds > 10)
            {
                staleKeys.Add(kvp.Key);
            }
        }

        foreach (var key in staleKeys)
        {
            timestampStore.Remove(key);
            latestStore.Remove(key);
        }
    }

    private void CleanupStaleWindowSamples(DateTimeOffset now, nint activeWindowHandle)
    {
        if (_dwmFrameSamples.Count <= 32 && _latestWindowFps.Count <= 32)
        {
            return;
        }

        var staleWindowHandles = _latestWindowFps
            .Where(kvp => kvp.Key != activeWindowHandle && now - kvp.Value.TimestampUtc > TimeSpan.FromSeconds(10))
            .Select(static kvp => kvp.Key)
            .ToArray();

        foreach (var handle in staleWindowHandles)
        {
            _latestWindowFps.Remove(handle);
            _dwmFrameSamples.Remove(handle);
        }
    }

    private readonly record struct TimedFpsSample(float Fps, DateTimeOffset TimestampUtc);
    private readonly record struct PresentedProcessSample(int ProcessId, OptionalFloat Fps, string Source);
    private readonly record struct PresentedContentCandidate(
        int ProcessId,
        string ProcessName,
        string Label,
        OptionalFloat Fps,
        string Source,
        ForegroundContentKind Kind);
}
