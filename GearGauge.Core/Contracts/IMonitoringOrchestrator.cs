using GearGauge.Core.Models;

namespace GearGauge.Core.Contracts;

public interface IMonitoringOrchestrator : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<HardwareMetrics> CaptureAsync(CancellationToken cancellationToken = default);
}
