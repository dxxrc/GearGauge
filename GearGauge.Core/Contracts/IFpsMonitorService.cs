using GearGauge.Core.Models;

namespace GearGauge.Core.Contracts;

public interface IFpsMonitorService : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<FpsMetrics> CaptureAsync(IReadOnlyList<MonitorInfo> monitors, CancellationToken cancellationToken = default);
}
