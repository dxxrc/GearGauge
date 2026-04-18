using GearGauge.Core.Contracts;
using GearGauge.Core.Models;

namespace GearGauge.Hardware;

public sealed class MonitoringOrchestrator : IMonitoringOrchestrator
{
    private readonly IHardwareMonitorService _hardwareMonitorService;
    private readonly IFpsMonitorService _fpsMonitorService;
    private readonly INetworkMonitorService _networkMonitorService;
    private bool _initialized;

    public MonitoringOrchestrator(
        IHardwareMonitorService hardwareMonitorService,
        IFpsMonitorService fpsMonitorService,
        INetworkMonitorService networkMonitorService)
    {
        _hardwareMonitorService = hardwareMonitorService;
        _fpsMonitorService = fpsMonitorService;
        _networkMonitorService = networkMonitorService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _hardwareMonitorService.InitializeAsync(cancellationToken);
        await _fpsMonitorService.InitializeAsync(cancellationToken);
        _initialized = true;
    }

    public async Task<HardwareMetrics> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _hardwareMonitorService.CaptureAsync(cancellationToken);
        snapshot.Fps = await _fpsMonitorService.CaptureAsync(snapshot.ActiveMonitors, cancellationToken);
        snapshot.NetworkAdapters = await _networkMonitorService.CaptureAsync(cancellationToken);
        snapshot.TimestampUtc = DateTimeOffset.UtcNow;
        return snapshot;
    }

    public async ValueTask DisposeAsync()
    {
        await _hardwareMonitorService.DisposeAsync();
        await _fpsMonitorService.DisposeAsync();
    }
}
