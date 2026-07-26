using DashDetective.Tabs.Performance;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="NvidiaGpuSensorReader"/>'s decode logic: choosing which of a GPU's thermal
/// sensors to believe, and rejecting readings outside a plausible range so a "not reported" zero blanks the
/// tile rather than being displayed. The NVAPI/NVML calls themselves are not unit-tested (they read live
/// hardware), mirroring how the raw samplers are left to the smoke run.</summary>
public class NvidiaGpuSensorReaderTests {
    private const int TargetNone = 0;
    private const int TargetGpu = 1;
    private const int TargetMemory = 2;
    private const int TargetBoard = 8;

    /// <summary>The GPU-core sensor is preferred over memory/board sensors wherever it sits in the array.</summary>
    [Theory]
    [InlineData(new[] { TargetGpu, TargetMemory, TargetBoard }, 3, 0)]
    [InlineData(new[] { TargetMemory, TargetGpu, TargetBoard }, 3, 1)]
    [InlineData(new[] { TargetMemory, TargetBoard, TargetGpu }, 3, 2)]
    [InlineData(new[] { TargetGpu, TargetNone, TargetNone }, 1, 0)]   // the RTX 3060: one sensor, target GPU
    public void SelectGpuSensorIndex_GpuTargetPresent_PrefersIt(int[] targets, int count, int expected) {
        Assert.Equal(expected, NvidiaGpuSensorReader.SelectGpuSensorIndex(count, targets));
    }

    /// <summary>With no GPU-core sensor reported, the first sensor is the best available answer.</summary>
    [Fact]
    public void SelectGpuSensorIndex_NoGpuTarget_FallsBackToTheFirstSensor() {
        Assert.Equal(0, NvidiaGpuSensorReader.SelectGpuSensorIndex(2, new[] { TargetMemory, TargetBoard }));
    }

    /// <summary>A GPU-core sensor beyond the reported count is not real and must not be selected.</summary>
    [Fact]
    public void SelectGpuSensorIndex_GpuTargetBeyondTheReportedCount_IsIgnored() {
        Assert.Equal(0, NvidiaGpuSensorReader.SelectGpuSensorIndex(1, new[] { TargetMemory, TargetGpu, TargetNone }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SelectGpuSensorIndex_NoSensorsReported_ReturnsMinusOne(int count) {
        Assert.Equal(-1, NvidiaGpuSensorReader.SelectGpuSensorIndex(count, new[] { TargetGpu, TargetNone, TargetNone }));
    }

    [Theory]
    [InlineData(41, 41)]      // the RTX 3060 at idle, cross-checked against nvidia-smi
    [InlineData(1, 1)]
    [InlineData(83, 83)]
    [InlineData(150, 150)]
    public void PlausibleCelsius_InRange_IsAccepted(int celsius, double expected) {
        Assert.Equal(expected, NvidiaGpuSensorReader.PlausibleCelsius(celsius));
    }

    [Theory]
    [InlineData(0)]           // "not reported"
    [InlineData(-40)]         // the sensor's default minimum, not a live reading
    [InlineData(151)]
    [InlineData(int.MaxValue)]
    public void PlausibleCelsius_OutOfRange_IsRejected(int celsius) {
        Assert.Null(NvidiaGpuSensorReader.PlausibleCelsius(celsius));
    }

    [Theory]
    [InlineData(16_420u, 16.42)]   // the RTX 3060 at idle, cross-checked against nvidia-smi
    [InlineData(100u, 0.1)]
    [InlineData(170_000u, 170.0)]
    [InlineData(2_000_000u, 2000.0)]
    public void PlausibleWatts_InRange_ConvertsMilliwattsToWatts(uint milliwatts, double expected) {
        Assert.Equal(expected, NvidiaGpuSensorReader.PlausibleWatts(milliwatts));
    }

    [Theory]
    [InlineData(0u)]               // "not reported"
    [InlineData(99u)]              // below a plausible board draw
    [InlineData(2_000_001u)]
    [InlineData(uint.MaxValue)]
    public void PlausibleWatts_OutOfRange_IsRejected(uint milliwatts) {
        Assert.Null(NvidiaGpuSensorReader.PlausibleWatts(milliwatts));
    }
}
