using GearGauge.Hardware;

namespace GearGauge.Tests;

public sealed class GpuKindClassifierTests
{
    [Theory]
    [InlineData("Intel(R) UHD Graphics 770", "Integrated")]
    [InlineData("AMD Radeon Graphics", "Integrated")]
    [InlineData("NVIDIA GeForce RTX 4080", "Discrete")]
    [InlineData("AMD Radeon RX 7900 XTX", "Discrete")]
    public void Classify_ReturnsExpectedKind(string name, string expected)
    {
        var actual = GpuKindClassifier.Classify(name);

        Assert.Equal(expected, actual);
    }
}
