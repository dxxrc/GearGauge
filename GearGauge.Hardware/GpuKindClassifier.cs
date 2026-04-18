namespace GearGauge.Hardware;

public static class GpuKindClassifier
{
    public static string Classify(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Unknown";
        }

        var normalized = name.ToLowerInvariant();

        if (normalized.Contains("intel") ||
            normalized.Contains("uhd") ||
            normalized.Contains("iris") ||
            normalized.Contains("radeon(tm) graphics") ||
            normalized.Contains("radeon graphics"))
        {
            return "Integrated";
        }

        if (normalized.Contains("nvidia") ||
            normalized.Contains("geforce") ||
            normalized.Contains("rtx") ||
            normalized.Contains("gtx") ||
            normalized.Contains("quadro") ||
            normalized.Contains("amd") ||
            normalized.Contains("radeon rx") ||
            normalized.Contains("arc "))
        {
            return "Discrete";
        }

        return "Unknown";
    }
}
