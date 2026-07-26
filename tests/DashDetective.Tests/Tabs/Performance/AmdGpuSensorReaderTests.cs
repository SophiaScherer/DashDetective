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
    private const int AsicPower = 23;
    private const int GfxPower = 30;
    private const int BoardPower = 73;

    // ADL_ASIC_* family-type bits.
    private const int Discrete = 1 << 0;
    private const int Integrated = 1 << 1;
    private const int Workstation = 1 << 2;
    private const int Fusion = 1 << 5;

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

    // ---- Discrete gate. Power is read only on discrete boards: on integrated parts ADL's power sensors
    // report whole-package power, measured climbing to ~50 W under a pure CPU load at 0 % GPU activity.

    /// <summary>The Radeon iGPU in the development machine reports INTEGRATED|FUSION (0x22).</summary>
    [Fact]
    public void IsDiscrete_TheIntegratedRadeonsRealValue_IsNotDiscrete() {
        Assert.False(AmdGpuSensorReader.IsDiscrete(0x22));
    }

    [Fact]
    public void IsDiscrete_DiscreteBitOnly_IsDiscrete() {
        Assert.True(AmdGpuSensorReader.IsDiscrete(Discrete));
    }

    /// <summary>A workstation board is still discrete silicon.</summary>
    [Fact]
    public void IsDiscrete_DiscreteWorkstation_IsDiscrete() {
        Assert.True(AmdGpuSensorReader.IsDiscrete(Discrete | Workstation));
    }

    /// <summary>An APU that also claims the discrete bit must not be trusted with power: integrated wins.</summary>
    [Theory]
    [InlineData(Discrete | Integrated)]
    [InlineData(Discrete | Fusion)]
    [InlineData(Discrete | Integrated | Fusion)]
    public void IsDiscrete_ClaimsBothDiscreteAndIntegrated_IsNotDiscrete(int asicTypes) {
        Assert.False(AmdGpuSensorReader.IsDiscrete(asicTypes));
    }

    /// <summary>ADL returns an error (so, no bits) for adapters it doesn't own; that must read as "not
    /// discrete" so an unknown board never reports power.</summary>
    [Fact]
    public void IsDiscrete_Undefined_IsNotDiscrete() {
        Assert.False(AmdGpuSensorReader.IsDiscrete(0));
    }

    // ---- Board power.

    [Theory]
    [InlineData(1)]
    [InlineData(230)]
    [InlineData(2000)]
    public void SelectBoardPower_PlausibleReading_IsAccepted(int watts) {
        var (supported, values) = Snapshot((BoardPower, watts));
        Assert.Equal(watts, AmdGpuSensorReader.SelectBoardPower(supported, values));
    }

    /// <summary>The Radeon iGPU here reports no board-power sensor at all — the common case.</summary>
    [Fact]
    public void SelectBoardPower_SensorUnsupported_ReturnsNull() {
        var (supported, values) = Snapshot((Gfx, 43));
        Assert.Null(AmdGpuSensorReader.SelectBoardPower(supported, values));
    }

    /// <summary>A grossly wrong unit scale (milliwatts, say) lands outside the window and blanks the tile
    /// rather than displaying a nonsense figure.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(230_000)]
    public void SelectBoardPower_ImplausibleReading_ReturnsNull(int watts) {
        var (supported, values) = Snapshot((BoardPower, watts));
        Assert.Null(AmdGpuSensorReader.SelectBoardPower(supported, values));
    }

    /// <summary>ASIC_POWER is deliberately never used as a fallback: on older discrete cards it is chip-only
    /// power, which would understate real draw while looking plausible.</summary>
    [Fact]
    public void SelectBoardPower_OnlyAsicPowerReported_ReturnsNullRatherThanFallingBack() {
        var (supported, values) = Snapshot((AsicPower, 180), (GfxPower, 150));
        Assert.Null(AmdGpuSensorReader.SelectBoardPower(supported, values));
    }

    [Fact]
    public void SelectBoardPower_SnapshotShorterThanTheSensorIndex_ReturnsNull() {
        Assert.Null(AmdGpuSensorReader.SelectBoardPower(new int[4], new int[4]));
    }
}
