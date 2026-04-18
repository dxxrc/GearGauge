using GearGauge.Core.Models;

namespace GearGauge.Hardware;

internal static class CpuMetricsEnricher
{
    public static void ApplyClockFallbacks(IDictionary<int, CoreMetrics> coreMap, CpuFrequencySnapshot snapshot)
    {
        foreach (var (index, clockGHz) in snapshot.CoreClocksGHz)
        {
            if (!coreMap.TryGetValue(index, out var core))
            {
                core = new CoreMetrics { Index = index };
                coreMap[index] = core;
            }

            if (!core.ClockGHz.HasValue && clockGHz.HasValue)
            {
                core.ClockGHz = clockGHz;
            }
        }
    }

    public static void ApplySharedTemperatureFallback(IEnumerable<CoreMetrics> cores, OptionalFloat packageTemperature)
    {
        if (!packageTemperature.HasValue)
        {
            return;
        }

        foreach (var core in cores)
        {
            if (!core.TemperatureCelsius.HasValue)
            {
                core.TemperatureCelsius = packageTemperature;
            }
        }
    }

    public static void ApplyEstimatedPowerFallback(IEnumerable<CoreMetrics> cores, OptionalFloat packagePower)
    {
        if (!packagePower.HasValue)
        {
            return;
        }

        var coreList = cores.ToArray();
        if (coreList.Length == 0 || coreList.Any(static core => core.PowerWatt.HasValue))
        {
            return;
        }

        var totalUsage = coreList.Sum(static core => Math.Max(core.UsagePercent, 0f));
        if (totalUsage <= 0)
        {
            var evenlyDistributedPower = packagePower.Value!.Value / coreList.Length;
            foreach (var core in coreList)
            {
                core.PowerWatt = evenlyDistributedPower;
            }

            return;
        }

        foreach (var core in coreList)
        {
            core.PowerWatt = packagePower.Value!.Value * (Math.Max(core.UsagePercent, 0f) / totalUsage);
        }
    }

    public static void ApplyHwInfoFallback(
        IDictionary<int, CoreMetrics> coreMap,
        ref OptionalFloat packageTemperature,
        ref OptionalFloat packagePower,
        HwInfoSnapshot hwInfo)
    {
        if (!packageTemperature.HasValue && hwInfo.CpuTemperatureCelsius.HasValue)
        {
            packageTemperature = hwInfo.CpuTemperatureCelsius;
        }

        if (!packagePower.HasValue && hwInfo.CpuPowerWatt.HasValue)
        {
            packagePower = hwInfo.CpuPowerWatt;
        }

        if (hwInfo.CpuClockGHz.HasValue)
        {
            foreach (var core in coreMap.Values)
            {
                if (!core.ClockGHz.HasValue)
                {
                    core.ClockGHz = hwInfo.CpuClockGHz;
                }
            }
        }
    }

    public static void ApplyWmiThermalFallback(
        IEnumerable<CoreMetrics> cores,
        ref OptionalFloat packageTemperature)
    {
        if (packageTemperature.HasValue)
        {
            return;
        }

        var wmiTemp = WmiThermalZoneReader.ReadCpuTemperature();
        if (!wmiTemp.HasValue)
        {
            return;
        }

        packageTemperature = wmiTemp;
        ApplySharedTemperatureFallback(cores, wmiTemp);
    }
}
