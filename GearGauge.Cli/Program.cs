using System.Text.Json;
using System.Text.Json.Serialization;
using GearGauge.Core.Contracts;
using GearGauge.Hardware;

var options = CliOptions.Parse(args);

if (options.DumpSensors)
{
    await DumpSensorsAsync();
    return;
}

await using IMonitoringOrchestrator orchestrator = new MonitoringOrchestrator(
    new HardwareMonitorService(new LibreHardwareMonitorAdapter(), new MonitorInfoProvider()),
    new FpsMonitorService(),
    new NetworkMonitorService());

await orchestrator.InitializeAsync();

var serializerOptions = new JsonSerializerOptions
{
    WriteIndented = options.Pretty,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

if (options.Watch)
{
    for (var i = 0; options.Iterations <= 0 || i < options.Iterations; i++)
    {
        var snapshot = await orchestrator.CaptureAsync();
        Console.WriteLine(JsonSerializer.Serialize(snapshot, serializerOptions));

        if (options.Iterations > 0 && i == options.Iterations - 1)
        {
            break;
        }

        await Task.Delay(options.IntervalMs);
    }
}
else
{
    var snapshot = await orchestrator.CaptureAsync();
    Console.WriteLine(JsonSerializer.Serialize(snapshot, serializerOptions));
}

static async Task DumpSensorsAsync()
{
    using var adapter = new LibreHardwareMonitorAdapter();
    await adapter.InitializeAsync();
    adapter.Update();

    foreach (var hardware in adapter.GetAllHardware())
    {
        Console.WriteLine($"[{hardware.HardwareType}] {hardware.Name}");
        foreach (var sensor in hardware.Sensors.OrderBy(static sensor => sensor.SensorType).ThenBy(static sensor => sensor.Name))
        {
            var value = sensor.Value.HasValue ? sensor.Value.Value.ToString("0.###") : "N/A";
            Console.WriteLine($"  - {sensor.SensorType,-12} {sensor.Name} = {value}");
        }
    }
}

internal sealed record CliOptions(bool Watch, int IntervalMs, int Iterations, bool Pretty, bool DumpSensors)
{
    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        var watch = args.Any(static a => a.Equals("--watch", StringComparison.OrdinalIgnoreCase));
        var pretty = args.Any(static a => a.Equals("--pretty", StringComparison.OrdinalIgnoreCase));
        var dumpSensors = args.Any(static a => a.Equals("--dump-sensors", StringComparison.OrdinalIgnoreCase));
        var intervalMs = ReadInt(args, "--interval-ms", 1000, minValue: 1);
        var iterations = ReadInt(args, "--iterations", 0, minValue: 0);

        return new CliOptions(watch, intervalMs, iterations, pretty, dumpSensors);
    }

    private static int ReadInt(IReadOnlyList<string> args, string name, int fallback, int minValue)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (!args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(args[i + 1], out var value) && value >= minValue)
            {
                return value;
            }
        }

        return fallback;
    }
}
