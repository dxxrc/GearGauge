using GearGauge.Core.Models;

namespace GearGauge.Core.Contracts;

public interface INetworkMonitorService
{
    Task<IReadOnlyList<NetworkAdapterMetrics>> CaptureAsync(CancellationToken cancellationToken = default);
}
