using System.Management;
using GearGauge.Core.Models;

namespace GearGauge.Hardware;

internal static class WmiThermalZoneReader
{
    public static OptionalFloat ReadCpuTemperature()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\wmi",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            float highest = float.MinValue;
            var found = false;

            foreach (var obj in searcher.Get().OfType<ManagementObject>())
            {
                var raw = obj["CurrentTemperature"];
                if (raw is not null && int.TryParse(raw.ToString(), out var tenthsKelvin))
                {
                    var celsius = (tenthsKelvin - 2732) / 10.0f;
                    if (celsius is > 0 and < 200)
                    {
                        highest = Math.Max(highest, celsius);
                        found = true;
                    }
                }
            }

            return found ? highest : OptionalFloat.None;
        }
        catch
        {
            return OptionalFloat.None;
        }
    }
}
