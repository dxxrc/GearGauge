using GearGauge.Core.Models;

namespace GearGauge.UI.Settings;

public static class UiValueFormatter
{
    public static string FormatPercent(float value, int decimals)
    {
        return $"{value.ToString(CreateFormat(decimals))}%";
    }

    public static string FormatTemperature(OptionalFloat value, int decimals)
    {
        return value.HasValue ? $"{value.Value!.Value.ToString(CreateFormat(decimals))} °C" : "N/A";
    }

    public static string FormatTemperature(OptionalFloat value, UiSettings settings, int decimals)
    {
        if (!value.HasValue)
        {
            return "N/A";
        }

        var temperature = value.Value!.Value;
        var unit = "°C";
        if (string.Equals(settings.TemperatureUnit, TemperatureUnits.Fahrenheit, StringComparison.OrdinalIgnoreCase))
        {
            temperature = (temperature * 9f / 5f) + 32f;
            unit = "°F";
        }

        return $"{temperature.ToString(CreateFormat(decimals))} {unit}";
    }

    public static string FormatPower(OptionalFloat value, int decimals)
    {
        return value.HasValue ? $"{value.Value!.Value.ToString(CreateFormat(decimals))} W" : "N/A";
    }

    public static string FormatCpuClock(OptionalFloat value, UiSettings settings)
    {
        if (!value.HasValue)
        {
            return "N/A";
        }

        var rawGHz = value.Value!.Value;
        return settings.CpuClockUnit.Equals(FrequencyUnit.MHz, StringComparison.OrdinalIgnoreCase)
            ? $"{(rawGHz * 1000f).ToString(CreateFormat(settings.CpuClockDecimals))} MHz"
            : $"{rawGHz.ToString(CreateFormat(settings.CpuClockDecimals))} GHz";
    }

    public static string FormatGpuClock(OptionalFloat value, UiSettings settings)
    {
        if (!value.HasValue)
        {
            return "N/A";
        }

        var rawMHz = value.Value!.Value;
        return settings.GpuClockUnit.Equals(FrequencyUnit.GHz, StringComparison.OrdinalIgnoreCase)
            ? $"{(rawMHz / 1000f).ToString(CreateFormat(settings.GpuClockDecimals))} GHz"
            : $"{rawMHz.ToString(CreateFormat(settings.GpuClockDecimals))} MHz";
    }

    public static string FormatMemory(float valueGb, int decimals)
    {
        return $"{valueGb.ToString(CreateFormat(decimals))} GB";
    }

    public static string FormatOptional(OptionalFloat value, string unit, int decimals)
    {
        return value.HasValue ? $"{value.Value!.Value.ToString(CreateFormat(decimals))} {unit}" : "N/A";
    }

    public static string FormatNetworkSpeed(double valueMbps, UiSettings settings)
    {
        var value = valueMbps;
        var unit = settings.NetworkSpeedUnit;

        switch (settings.NetworkSpeedUnit)
        {
            case NetworkSpeedUnits.Kbps:
                value = valueMbps * 1000d;
                unit = NetworkSpeedUnits.Kbps;
                break;
            case NetworkSpeedUnits.Gbps:
                value = valueMbps / 1000d;
                unit = NetworkSpeedUnits.Gbps;
                break;
            case NetworkSpeedUnits.KBps:
                value = valueMbps / 8d * 1000d;
                unit = NetworkSpeedUnits.KBps;
                break;
            case NetworkSpeedUnits.MBps:
                value = valueMbps / 8d;
                unit = NetworkSpeedUnits.MBps;
                break;
            default:
                unit = NetworkSpeedUnits.Mbps;
                break;
        }

        return $"{value.ToString(CreateFormat(settings.NetworkSpeedDecimals))} {unit}";
    }

    private static string CreateFormat(int decimals)
    {
        return decimals <= 0 ? "0" : $"0.{new string('0', decimals)}";
    }
}
