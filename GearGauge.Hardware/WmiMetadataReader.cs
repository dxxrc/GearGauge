using System.Management;

namespace GearGauge.Hardware;

public static class WmiMetadataReader
{
    public static string GetCpuModelName()
    {
        var value = QueryFirstString("SELECT Name FROM Win32_Processor", "Name");
        return string.IsNullOrWhiteSpace(value) ? "Unknown CPU" : value;
    }

    public static int GetPhysicalCoreCount()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor");
            foreach (var obj in searcher.Get().OfType<ManagementObject>())
            {
                var value = obj["NumberOfCores"]?.ToString();
                if (int.TryParse(value, out var cores) && cores > 0)
                {
                    return cores;
                }
            }
        }
        catch
        {
            // Ignore WMI failures.
        }

        return 0;
    }

    public static string GetMemoryModelName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Manufacturer, PartNumber, SMBIOSMemoryType, Capacity, Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory");

            var dimmInfos = new List<DimmInfo>();
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var obj in searcher.Get().OfType<ManagementObject>())
            {
                var manufacturer = obj["Manufacturer"]?.ToString()?.Trim();
                var partNumber = obj["PartNumber"]?.ToString()?.Trim();
                var typeCode = obj["SMBIOSMemoryType"]?.ToString()?.Trim();
                var capacityBytes = obj["Capacity"]?.ToString();
                var speedMhz = obj["ConfiguredClockSpeed"]?.ToString()
                               ?? obj["Speed"]?.ToString();

                var typeName = MapMemoryType(typeCode);
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    types.Add(typeName);
                }

                var hasPartNumber = !string.IsNullOrWhiteSpace(partNumber);
                var hasManufacturer = !string.IsNullOrWhiteSpace(manufacturer);
                var hasCapacity = long.TryParse(capacityBytes, out var capacityBytesValue) && capacityBytesValue > 0;
                var hasSpeed = int.TryParse(speedMhz, out var speed) && speed > 0;

                string label;
                if (hasPartNumber)
                {
                    label = string.Join(" ", new[] { manufacturer, partNumber }
                        .Where(static s => !string.IsNullOrWhiteSpace(s)));
                }
                else if (hasManufacturer && hasCapacity)
                {
                    var capacityGB = capacityBytesValue / 1024.0 / 1024.0 / 1024.0;
                    label = $"{manufacturer} {capacityGB:0}GB";
                }
                else if (hasCapacity)
                {
                    var capacityGB = capacityBytesValue / 1024.0 / 1024.0 / 1024.0;
                    label = $"{capacityGB:0}GB";
                }
                else if (hasManufacturer)
                {
                    label = manufacturer ?? string.Empty;
                }
                else
                {
                    label = string.Empty;
                }

                dimmInfos.Add(new DimmInfo(label!, hasSpeed ? speed : 0));
            }

            var prefix = types.Count switch
            {
                0 => "Memory",
                1 => types.First(),
                _ => string.Join("/", types.Order())
            };

            var distinctLabels = dimmInfos
                .Select(static d => d.Label)
                .Where(static l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctLabels.Count == 0)
            {
                return prefix;
            }

            var result = $"{prefix}: {string.Join(" | ", distinctLabels)}";

            // Append count and speed if multiple identical DIMMs.
            if (dimmInfos.Count > 1)
            {
                var speeds = dimmInfos.Select(static d => d.Speed).Where(static s => s > 0).Distinct().ToArray();
                if (speeds.Length == 1)
                {
                    result += $" x {dimmInfos.Count} @ {speeds[0]}MHz";
                }
                else
                {
                    result += $" x {dimmInfos.Count}";
                }
            }

            return result;
        }
        catch
        {
            return "Memory";
        }
    }

    public static string QueryFirstString(string query, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (var obj in searcher.Get().OfType<ManagementObject>())
            {
                var value = obj[propertyName]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        catch
        {
            // Ignore WMI failures and let callers fall back.
        }

        return string.Empty;
    }

    private static string? MapMemoryType(string? typeCode)
    {
        return typeCode switch
        {
            "20" => "DDR",
            "21" => "DDR2",
            "24" => "DDR3",
            "26" => "DDR4",
            "34" => "DDR5",
            _ => null
        };
    }

    private readonly record struct DimmInfo(string Label, int Speed);
}
