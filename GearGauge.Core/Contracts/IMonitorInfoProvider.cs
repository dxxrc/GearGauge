using GearGauge.Core.Models;

namespace GearGauge.Core.Contracts;

public interface IMonitorInfoProvider
{
    IReadOnlyList<MonitorInfo> GetMonitors();
}
