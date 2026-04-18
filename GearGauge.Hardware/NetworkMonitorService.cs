using System.Net.NetworkInformation;
using GearGauge.Core.Contracts;
using GearGauge.Core.Models;

namespace GearGauge.Hardware;

public sealed class NetworkMonitorService : INetworkMonitorService
{
    private readonly Dictionary<string, AdapterSample> _samples = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<NetworkAdapterMetrics>> CaptureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var adapters = new List<NetworkAdapterMetrics>();
        var activeAdapterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            IPv4InterfaceStatistics statistics;
            try
            {
                statistics = nic.GetIPv4Statistics();
            }
            catch
            {
                continue;
            }

            var id = nic.Id;
            activeAdapterIds.Add(id);
            var sent = statistics.BytesSent;
            var received = statistics.BytesReceived;

            var uploadMbps = 0d;
            var downloadMbps = 0d;
            if (_samples.TryGetValue(id, out var previous))
            {
                var elapsed = now - previous.Timestamp;
                uploadMbps = NetworkRateCalculator.ToMbps(previous.BytesSent, sent, elapsed);
                downloadMbps = NetworkRateCalculator.ToMbps(previous.BytesReceived, received, elapsed);
            }

            _samples[id] = new AdapterSample(sent, received, now);

            adapters.Add(new NetworkAdapterMetrics
            {
                AdapterId = id,
                AdapterName = nic.Name,
                IsUp = nic.OperationalStatus == OperationalStatus.Up,
                UploadMbps = uploadMbps,
                DownloadMbps = downloadMbps,
                Type = FormatInterfaceType(nic.NetworkInterfaceType)
            });
        }

        foreach (var staleAdapterId in GetStaleSampleIds(_samples.Keys, activeAdapterIds))
        {
            _samples.Remove(staleAdapterId);
        }

        var selectedId = adapters
            .Where(static a => a.IsUp)
            .OrderBy(static a => IsLikelyVirtualAdapter(a.AdapterName, a.Type))
            .ThenByDescending(a => a.DownloadMbps + a.UploadMbps)
            .ThenBy(a => a.AdapterName, StringComparer.OrdinalIgnoreCase)
            .Select(static a => a.AdapterId)
            .FirstOrDefault();

        foreach (var adapter in adapters)
        {
            adapter.IsSelected = adapter.AdapterId.Equals(selectedId, StringComparison.OrdinalIgnoreCase);
        }

        return Task.FromResult<IReadOnlyList<NetworkAdapterMetrics>>(adapters
            .OrderByDescending(static a => a.IsSelected)
            .ThenByDescending(static a => a.IsUp)
            .ThenBy(static a => a.AdapterName, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    private readonly record struct AdapterSample(long BytesSent, long BytesReceived, DateTimeOffset Timestamp);

    internal static IReadOnlyList<string> GetStaleSampleIds(IEnumerable<string> cachedAdapterIds, ISet<string> activeAdapterIds)
    {
        return cachedAdapterIds
            .Where(id => !activeAdapterIds.Contains(id))
            .ToArray();
    }

    private static string FormatInterfaceType(NetworkInterfaceType type)
    {
        return Enum.IsDefined(type)
            ? type.ToString()
            : $"Unknown({(int)type})";
    }

    private static bool IsLikelyVirtualAdapter(string adapterName, string type)
    {
        var normalized = adapterName.ToLowerInvariant();
        return normalized.Contains("vmware") ||
               normalized.Contains("virtual") ||
               normalized.Contains("vbox") ||
               normalized.Contains("tailscale") ||
               normalized.Contains("meta") ||
               type.Contains("Unknown", StringComparison.OrdinalIgnoreCase);
    }
}
