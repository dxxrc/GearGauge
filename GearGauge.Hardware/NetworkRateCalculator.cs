namespace GearGauge.Hardware;

public static class NetworkRateCalculator
{
    public static double ToMbps(long previousBytes, long currentBytes, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero || currentBytes < previousBytes)
        {
            return 0;
        }

        var bytesPerSecond = (currentBytes - previousBytes) / elapsed.TotalSeconds;
        var bitsPerSecond = bytesPerSecond * 8d;
        return bitsPerSecond / 1_000_000d;
    }
}
