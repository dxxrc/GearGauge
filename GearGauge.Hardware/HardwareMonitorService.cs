using System.Globalization;
using System.Text.RegularExpressions;
using GearGauge.Core.Contracts;
using GearGauge.Core.Helpers;
using GearGauge.Core.Models;
using LibreHardwareMonitor.Hardware;

namespace GearGauge.Hardware;

public sealed partial class HardwareMonitorService : IHardwareMonitorService
{
    private readonly LibreHardwareMonitorAdapter _adapter;
    private readonly CpuFrequencyReader _cpuFrequencyReader;
    private readonly IMonitorInfoProvider _monitorInfoProvider;
    private string _cpuModelName = "Unknown CPU";
    private string _memoryModelName = "Memory";
    private int _physicalCoreCount;
    private bool _initialized;
    private DateTime _lastHwInfoAttempt = DateTime.MinValue;
    private HwInfoSnapshot? _cachedHwInfo;

    public HardwareMonitorService(
        LibreHardwareMonitorAdapter adapter,
        IMonitorInfoProvider monitorInfoProvider)
    {
        _adapter = adapter;
        _cpuFrequencyReader = new CpuFrequencyReader();
        _monitorInfoProvider = monitorInfoProvider;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        var metadataTask = Task.Run(LoadMetadata, cancellationToken);

        try
        {
            await _adapter.InitializeAsync(cancellationToken);
        }
        catch (Exception)
        {
            // LHM native DLLs may be blocked by Smart App Control, continue without it.
        }

        await metadataTask;
        _initialized = true;
    }

    public Task<HardwareMetrics> CaptureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_adapter.IsAvailable)
        {
            _adapter.Update();
        }

        var (cpu, dataSources) = CaptureCpu();
        var metrics = new HardwareMetrics
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Cpu = cpu,
            Gpus = CaptureGpus(),
            Memory = MemoryStatusReader.Read(_memoryModelName),
            ActiveMonitors = _monitorInfoProvider.GetMonitors(),
            IsElevated = AdminStatus.IsElevated,
            DataSourceInfo = string.Join(" + ", dataSources)
        };

        return Task.FromResult(metrics);
    }

    public ValueTask DisposeAsync()
    {
        _cpuFrequencyReader.Dispose();
        _adapter.Dispose();
        return ValueTask.CompletedTask;
    }

    private void LoadMetadata()
    {
        _cpuModelName = WmiMetadataReader.GetCpuModelName();
        _memoryModelName = WmiMetadataReader.GetMemoryModelName();
        _physicalCoreCount = WmiMetadataReader.GetPhysicalCoreCount();
    }

    private (CpuMetrics Cpu, List<string> DataSources) CaptureCpu()
    {
        var dataSources = new List<string>();
        var hardware = _adapter.GetCpuHardware();

        if (_adapter.IsAvailable && hardware is not null)
        {
            dataSources.Add("LHM");
        }

        var sensors = hardware is not null ? _adapter.GetSensors(hardware) : [];
        var loadSensors = sensors.Where(s => s.SensorType == SensorType.Load).ToArray();
        var clockSensors = sensors.Where(s => s.SensorType == SensorType.Clock).ToArray();
        var tempSensors = sensors.Where(s => s.SensorType == SensorType.Temperature).ToArray();
        var powerSensors = sensors.Where(s => s.SensorType == SensorType.Power).ToArray();
        var frequencySnapshot = _cpuFrequencyReader.ReadSnapshot();

        if (frequencySnapshot.CoreClocksGHz.Count > 0)
        {
            dataSources.Add("PDH");
        }

        var cpuUsage = GetSensorValue(loadSensors,
            s => s.Name.Contains("CPU Total", StringComparison.OrdinalIgnoreCase));

        if (!cpuUsage.HasValue)
        {
            var coreLoads = loadSensors
                .Where(s => TryGetCoreIndex(s.Name, out _))
                .Select(s => s.Value ?? 0f)
                .ToArray();

            cpuUsage = coreLoads.Length == 0 ? OptionalFloat.None : coreLoads.Average();
        }

        var coreMap = new Dictionary<int, CoreMetrics>();
        FillCoreValues(coreMap, loadSensors, static (core, value) => core.UsagePercent = value);
        FillCoreValues(coreMap, clockSensors, static (core, value) => core.ClockGHz = value / 1000f, static value => float.IsFinite(value) && value > 0);
        FillCoreValues(coreMap, tempSensors, static (core, value) => core.TemperatureCelsius = value, static value => float.IsFinite(value) && value > 0);
        FillCoreValues(coreMap, powerSensors, static (core, value) => core.PowerWatt = value, static value => float.IsFinite(value) && value > 0);
        CpuMetricsEnricher.ApplyClockFallbacks(coreMap, frequencySnapshot);

        var packageTemperature = GetSensorValue(tempSensors,
            s => s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                 s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                 s.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
                 s.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase));

        var packagePower = GetSensorValue(powerSensors,
            s => s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                 s.Name.Contains("PPT", StringComparison.OrdinalIgnoreCase) ||
                 s.Name.Contains("CPU Package", StringComparison.OrdinalIgnoreCase) ||
                 s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase));

        CpuMetricsEnricher.ApplySharedTemperatureFallback(coreMap.Values, packageTemperature);
        CpuMetricsEnricher.ApplyEstimatedPowerFallback(coreMap.Values, packagePower);

        if ((DateTime.UtcNow - _lastHwInfoAttempt).TotalSeconds >= 5)
        {
            _cachedHwInfo = HwInfoSharedMemoryReader.TryReadSnapshot();
            _lastHwInfoAttempt = DateTime.UtcNow;
        }

        if (_cachedHwInfo is not null)
        {
            var hadTemp = packageTemperature.HasValue;
            var hadPower = packagePower.HasValue;
            CpuMetricsEnricher.ApplyHwInfoFallback(coreMap, ref packageTemperature, ref packagePower, _cachedHwInfo);
            if (!hadTemp && packageTemperature.HasValue || !hadPower && packagePower.HasValue)
            {
                dataSources.Add("HWiNFO");
            }
        }

        var hadPackageTemp = packageTemperature.HasValue;
        CpuMetricsEnricher.ApplyWmiThermalFallback(coreMap.Values, ref packageTemperature);
        if (!hadPackageTemp && packageTemperature.HasValue)
        {
            dataSources.Add("WMI");
        }

        if (_physicalCoreCount > 0 && coreMap.Count > _physicalCoreCount)
        {
            var collapsed = new Dictionary<int, CoreMetrics>();
            foreach (var (index, core) in coreMap)
            {
                var physicalIndex = index % _physicalCoreCount;
                if (!collapsed.TryGetValue(physicalIndex, out var physicalCore))
                {
                    physicalCore = new CoreMetrics { Index = physicalIndex };
                    collapsed[physicalIndex] = physicalCore;
                }

                MergeCoreData(physicalCore, core);
            }

            coreMap.Clear();
            foreach (var (idx, c) in collapsed)
            {
                coreMap[idx] = c;
            }
        }

        var coreClockValues = coreMap.Values
            .Where(c => c.ClockGHz.HasValue)
            .Select(c => c.ClockGHz.Value!.Value)
            .ToArray();

        if (!packagePower.HasValue)
        {
            var measuredCorePowerValues = coreMap.Values
                .Where(c => c.PowerWatt.HasValue)
                .Select(c => c.PowerWatt.Value!.Value)
                .Where(static value => value > 0)
                .ToArray();

            if (measuredCorePowerValues.Length > 0)
            {
                packagePower = measuredCorePowerValues.Sum();
            }
        }

        return (new CpuMetrics
        {
            ModelName = hardware is not null
                ? (string.IsNullOrWhiteSpace(_cpuModelName) ? hardware.Name : _cpuModelName)
                : _cpuModelName,
            UsagePercent = cpuUsage.HasValue ? cpuUsage.Value!.Value : 0,
            TemperatureCelsius = packageTemperature,
            PowerWatt = packagePower,
            ClockGHz = coreClockValues.Length == 0 ? frequencySnapshot.PackageClockGHz : coreClockValues.Average(),
            Cores = coreMap.Values.OrderBy(c => c.Index).ToArray()
        }, dataSources);
    }

    private IReadOnlyList<GpuMetrics> CaptureGpus()
    {
        var gpus = new List<GpuMetrics>();

        foreach (var hardware in _adapter.GetGpuHardware())
        {
            var sensors = _adapter.GetSensors(hardware);

            var usage = GetSensorValue(sensors, SensorType.Load,
                s => s.Name.Contains("D3D 3D", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("Core Load", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Equals("GPU", StringComparison.OrdinalIgnoreCase));
            if (!usage.HasValue) usage = GetFirstSensorValue(sensors, SensorType.Load);

            var temperature = GetSensorValue(sensors, SensorType.Temperature,
                s => s.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("GPU Temperature", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("Hotspot", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Equals("GPU", StringComparison.OrdinalIgnoreCase));
            if (!temperature.HasValue) temperature = GetFirstSensorValue(sensors, SensorType.Temperature);

            var power = GetSensorValue(sensors, SensorType.Power,
                s => s.Name.Contains("GPU Power", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Equals("GPU", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Equals("GPU Chip", StringComparison.OrdinalIgnoreCase));
            if (!power.HasValue) power = GetFirstSensorValue(sensors, SensorType.Power);

            var clock = GetSensorValue(sensors, SensorType.Clock,
                s => s.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("Core Clock", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Equals("GPU", StringComparison.OrdinalIgnoreCase));
            if (!clock.HasValue) clock = GetFirstSensorValue(sensors, SensorType.Clock);

            gpus.Add(new GpuMetrics
            {
                Id = hardware.Identifier.ToString(),
                Name = hardware.Name,
                Kind = GpuKindClassifier.Classify(hardware.Name),
                IsActive = (usage.HasValue && usage.Value > 0.5f) ||
                           (power.HasValue && power.Value > 1f),
                UsagePercent = usage.HasValue ? usage.Value!.Value : 0,
                TemperatureCelsius = temperature,
                PowerWatt = power,
                ClockMHz = clock
            });
        }

        return gpus;
    }

    private static void FillCoreValues(
        IDictionary<int, CoreMetrics> coreMap,
        IEnumerable<ISensor> sensors,
        Action<CoreMetrics, float> setter)
    {
        FillCoreValues(coreMap, sensors, setter, static _ => true);
    }

    private static void FillCoreValues(
        IDictionary<int, CoreMetrics> coreMap,
        IEnumerable<ISensor> sensors,
        Action<CoreMetrics, float> setter,
        Func<float, bool> valuePredicate)
    {
        foreach (var sensor in sensors)
        {
            if (!sensor.Value.HasValue ||
                !valuePredicate(sensor.Value.Value) ||
                !TryGetCoreIndex(sensor.Name, out var index))
            {
                continue;
            }

            if (!coreMap.TryGetValue(index, out var core))
            {
                core = new CoreMetrics { Index = index };
                coreMap[index] = core;
            }

            setter(core, sensor.Value.Value);
        }
    }

    private static OptionalFloat GetSensorValue(
        IEnumerable<ISensor> sensors,
        SensorType sensorType,
        Func<ISensor, bool> predicate)
    {
        return GetSensorValue(sensors.Where(s => s.SensorType == sensorType).ToArray(), predicate);
    }

    private static OptionalFloat GetSensorValue(IReadOnlyList<ISensor> sensors, Func<ISensor, bool> predicate)
    {
        var match = sensors
            .Where(predicate)
            .Where(static s => s.Value.HasValue && s.Value.Value > 0)
            .OrderByDescending(static s => s.Value!.Value)
            .FirstOrDefault();

        if (match?.Value is { } value)
        {
            return value;
        }

        match = sensors
            .Where(predicate)
            .FirstOrDefault(static s => s.Value.HasValue);

        return match?.Value is { } fallback && fallback > 0
            ? fallback
            : OptionalFloat.None;
    }

    private static OptionalFloat GetFirstSensorValue(IReadOnlyList<ISensor> sensors, SensorType sensorType)
    {
        var match = sensors
            .FirstOrDefault(s => s.SensorType == sensorType && s.Value.HasValue && s.Value.Value > 0);

        return match?.Value is { } v ? v : OptionalFloat.None;
    }

    private static bool TryGetCoreIndex(string sensorName, out int index)
    {
        var match = CoreIndexRegex().Match(sensorName);
        if (match.Success &&
            int.TryParse(match.Groups["index"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
        {
            return true;
        }

        index = -1;
        return false;
    }

    [GeneratedRegex(@"(?:CPU\s+)?Core\s+#?(?<index>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CoreIndexRegex();

    private static void MergeCoreData(CoreMetrics target, CoreMetrics source)
    {
        target.UsagePercent = Math.Max(target.UsagePercent, source.UsagePercent);

        if (source.ClockGHz.HasValue)
        {
            target.ClockGHz = target.ClockGHz.HasValue
                ? Math.Max(target.ClockGHz.Value!.Value, source.ClockGHz.Value!.Value)
                : source.ClockGHz;
        }

        if (source.TemperatureCelsius.HasValue && !target.TemperatureCelsius.HasValue)
        {
            target.TemperatureCelsius = source.TemperatureCelsius;
        }

        if (source.PowerWatt.HasValue && !target.PowerWatt.HasValue)
        {
            target.PowerWatt = source.PowerWatt;
        }
    }
}
