using GearGauge.Core.Models;

namespace GearGauge.Core.Contracts;

public interface IHardwareMonitorService : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<HardwareMetrics> CaptureAsync(CancellationToken cancellationToken = default);
}
