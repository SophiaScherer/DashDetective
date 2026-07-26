using DashDetective.Tabs.Performance;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="AmdGpuSensorReader.SelectTemperature"/>: choosing a GPU temperature out of ADL's
/// PMLOG snapshot, which is indexed by sensor type and reports which sensors a given board actually has. The
/// ADL calls themselves are not unit-tested (they read live hardware), mirroring how the raw samplers are left
/// to the smoke run.</summary>
public class AmdGpuSensorReaderTests {
    private const int SensorCount = 256;
    private const int Edge = 8;
    private const int Hotspot = 27;
    private const int Gfx = 28;

    /// <summary>Builds a PMLOG snapshot in which only the given sensors are supported.</summary>
    private static (int[] Supported, int[] Values) Snapshot(params (int Sensor, int Value)[] sensors) {
        var supported = new int[SensorCount];
        var values = new int[SensorCount];
        foreach (var (sensor, value) in sensors) {
            supported[sensor] = 1;
            values[sensor] = value;
        }
        return (supported, values);
    }

    /// <summary>A discrete board reports the edge sensor, which is the conventional GPU temperature.</summary>
    [Fact]
    public void SelectTemperature_EdgeSupported_PrefersIt() {
        var (supported, values) = Snapshot((Edge, 61), (Gfx, 64), (Hotspot, 78));
        Assert.Equal(61, AmdGpuSensorReader.SelectTemperature(supported, values));
    }

    /// <summary>The Radeon iGPU in the development machine: no edge sensor, GFX reads a plausible value.</summary>
    [Fact]
    public void SelectTemperature_NoEdgeSensor_FallsBackToGfx() {
        var (supported, values) = Snapshot((Gfx, 43), (Hotspot, 55));
        Assert.Equal(43, AmdGpuSensorReader.SelectTemperature(supported, values));
    }

    [Fact]
    public void SelectTemperature_OnlyHotspot_UsesIt() {
        var (supported, values) = Snapshot((Hotspot, 72));
        Assert.Equal(72, AmdGpuSensorReader.SelectTemperature(supported, values));
    }

    /// <summary>A supported-but-implausible reading is skipped in favour of the next sensor, rather than being
    /// displayed.</summary>
    [Fact]
    public void SelectTemperature_PreferredSensorImplausible_FallsThroughToTheNext() {
        var (supported, values) = Snapshot((Edge, 0), (Gfx, 43));
        Assert.Equal(43, AmdGpuSensorReader.SelectTemperature(supported, values));
    }

    [Fact]
    public void SelectTemperature_NoTemperatureSensorsSupported_ReturnsNull() {
        var (supported, values) = Snapshot((19, 0), (23, 54));   // activity + power only
        Assert.Null(AmdGpuSensorReader.SelectTemperature(supported, values));
    }

    /// <summary>A sensor flagged unsupported must be ignored even when its slot holds a plausible number.</summary>
    [Fact]
    public void SelectTemperature_UnsupportedSensorWithStaleValue_IsIgnored() {
        var supported = new int[SensorCount];
        var values = new int[SensorCount];
        values[Edge] = 61;   // value present but Supported stays 0
        Assert.Null(AmdGpuSensorReader.SelectTemperature(supported, values));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-40)]
    [InlineData(151)]
    public void SelectTemperature_EveryReadingImplausible_ReturnsNull(int celsius) {
        var (supported, values) = Snapshot((Edge, celsius), (Gfx, celsius), (Hotspot, celsius));
        Assert.Null(AmdGpuSensorReader.SelectTemperature(supported, values));
    }

    /// <summary>A short snapshot must not index past its end.</summary>
    [Fact]
    public void SelectTemperature_SnapshotShorterThanTheSensorIndices_ReturnsNull() {
        Assert.Null(AmdGpuSensorReader.SelectTemperature(new int[4], new int[4]));
    }
}
