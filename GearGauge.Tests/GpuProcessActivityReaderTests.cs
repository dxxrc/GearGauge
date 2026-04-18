using GearGauge.Hardware;

namespace GearGauge.Tests;

public sealed class GpuProcessActivityReaderTests
{
    [Fact]
    public void TryParseInstanceName_ParsesProcessIdAndEngineType()
    {
        var success = GpuProcessActivityReader.TryParseInstanceName(
            "pid_8580_luid_0x00000000_0x000106c4_phys_0_eng_0_engtype_3d",
            out var processId,
            out var engineType);

        Assert.True(success);
        Assert.Equal(8580, processId);
        Assert.Equal("3d", engineType, ignoreCase: true);
    }

    [Fact]
    public void TryParseInstanceName_RejectsInvalidInstanceNames()
    {
        var success = GpuProcessActivityReader.TryParseInstanceName(
            "luid_0x00000000_phys_0_eng_0",
            out var processId,
            out var engineType);

        Assert.False(success);
        Assert.Equal(0, processId);
        Assert.Equal(string.Empty, engineType);
    }
}
