using GearGauge.Core.Contracts;
using GearGauge.Core.Models;
using GearGauge.Hardware;

namespace GearGauge.Tests;

public sealed class MonitoringOrchestratorTests
{
    [Fact]
    public async Task CaptureAsync_ComposesAllSourcesIntoSingleSnapshot()
    {
        var hardware = new FakeHardwareMonitorService();
        var fps = new FakeFpsMonitorService();
        var network = new FakeNetworkMonitorService();
        await using var orchestrator = new MonitoringOrchestrator(hardware, fps, network);

        await orchestrator.InitializeAsync();
        var snapshot = await orchestrator.CaptureAsync();

        Assert.Equal("Test CPU", snapshot.Cpu.ModelName);
        Assert.Single(snapshot.Gpus);
        Assert.Equal(144, snapshot.Fps.DisplayOutputFps.Value);
        Assert.Single(snapshot.NetworkAdapters);
        Assert.True(hardware.Initialized);
        Assert.True(fps.Initialized);
    }

    private sealed class FakeHardwareMonitorService : IHardwareMonitorService
    {
        public bool Initialized { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Initialized = true;
            return Task.CompletedTask;
        }

        public Task<HardwareMetrics> CaptureAsync(CancellationToken cancellationToken = default)
        {
            var metrics = new HardwareMetrics
            {
                Cpu = new CpuMetrics { ModelName = "Test CPU", UsagePercent = 12.5f },
                Gpus = new[] { new GpuMetrics { Id = "gpu0", Name = "Test GPU", UsagePercent = 45 } },
                Memory = new MemoryMetrics { ModelName = "DDR5", TotalGB = 32, UsedGB = 12, AvailableGB = 20, UsagePercent = 37.5f },
                ActiveMonitors = new[] { new MonitorInfo { Index = 0, FriendlyName = "Test Monitor", RefreshRate = 144, IsPrimary = true } }
            };

            return Task.FromResult(metrics);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeFpsMonitorService : IFpsMonitorService
    {
        public bool Initialized { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Initialized = true;
            return Task.CompletedTask;
        }

        public Task<FpsMetrics> CaptureAsync(IReadOnlyList<MonitorInfo> monitors, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FpsMetrics
            {
                DisplayOutputFps = monitors[0].RefreshRate,
                GameFps = 120,
                ActiveContent = "Game: testgame",
                ProviderStatus = "Test"
            });
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeNetworkMonitorService : INetworkMonitorService
    {
        public Task<IReadOnlyList<NetworkAdapterMetrics>> CaptureAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<NetworkAdapterMetrics> result =
            [
                new NetworkAdapterMetrics
                {
                    AdapterId = "nic0",
                    AdapterName = "Ethernet",
                    IsSelected = true,
                    IsUp = true,
                    UploadMbps = 10,
                    DownloadMbps = 100
                }
            ];

            return Task.FromResult(result);
        }
    }
}
