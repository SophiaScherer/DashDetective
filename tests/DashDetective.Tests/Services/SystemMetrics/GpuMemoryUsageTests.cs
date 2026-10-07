using DashDetective.Services.SystemMetrics;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Pins <see cref="GpuMemoryUsage"/>: memory instances normalize to the same LUID token the engine
/// counter yields, sum across physical indices, and join onto the engine samples without ever adding an
/// adapter or turning an unknown usage into 0.</summary>
public class GpuMemoryUsageTests {
    private const string Nvidia = "luid_0x00000000_0x0000e7be";
    private const string Amd = "luid_0x00000000_0x0000f83d";

    private static readonly IReadOnlyDictionary<string, double> NoEngines = new Dictionary<string, double>();

    [Theory]
    [InlineData("luid_0x00000000_0x0000E7BE_phys_0")]
    [InlineData("luid_0x00000000_0x0000e7be_phys_0")]
    [InlineData("LUID_0X00000000_0X0000E7BE_PHYS_0")]
    public void ByAdapter_AnyCasing_JoinsTheLowerCaseToken(string name) {
        Assert.Equal(1185382400UL, GpuMemoryUsage.ByAdapter([(name, 1185382400)])[Nvidia]);
    }

    [Fact]
    public void ByAdapter_SumsAcrossPhysicalIndices() {
        var readings = new (string?, double)[] {
            ("luid_0x00000000_0x0000e7be_phys_0", 100),
            ("luid_0x00000000_0x0000e7be_phys_1", 250),
            ("luid_0x00000000_0x0000f83d_phys_0", 7),
        };

        var byAdapter = GpuMemoryUsage.ByAdapter(readings);

        Assert.Equal(350UL, byAdapter[Nvidia]);
        Assert.Equal(7UL, byAdapter[Amd]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("luid_0x00000000_0x0000e7be")]
    [InlineData("luid_0xZZ_0x0000e7be_phys_0")]
    public void ByAdapter_UnparseableName_IsSkipped(string? name) {
        Assert.Empty(GpuMemoryUsage.ByAdapter([(name, 5)]));
    }

    [Fact]
    public void ByAdapter_NegativeOrNaNReading_IsSkipped() {
        var readings = new (string?, double)[] {
            ("luid_0x00000000_0x0000e7be_phys_0", -1),
            ("luid_0x00000000_0x0000f83d_phys_0", double.NaN),
        };

        Assert.Empty(GpuMemoryUsage.ByAdapter(readings));
    }

    [Fact]
    public void ByAdapter_ZeroReading_IsKeptAsZero() {
        Assert.Equal(0UL, GpuMemoryUsage.ByAdapter([("luid_0x00000000_0x0000e7be_phys_0", 0)])[Nvidia]);
    }

    [Fact]
    public void Join_FillsUsageOnTheMatchingAdapterAndKeepsItsEngines() {
        var samples = new Dictionary<string, GpuAdapterSample> {
            [Nvidia] = new(42, new Dictionary<string, double> { ["3D"] = 42 }),
        };

        var joined = GpuMemoryUsage.Join(samples, new Dictionary<string, ulong> { [Nvidia] = 1000 });

        Assert.Equal(1000UL, joined[Nvidia].DedicatedUsedBytes);
        Assert.Equal(42, joined[Nvidia].Overall);
        Assert.Equal(42, joined[Nvidia].Engines["3D"]);
    }

    /// <summary>The inventory keys GPUs off the engine counter, so a memory-only LUID must not mint a
    /// sample.</summary>
    [Fact]
    public void Join_MemoryOnlyAdapter_IsIgnored() {
        var samples = new Dictionary<string, GpuAdapterSample> { [Nvidia] = new(10, NoEngines) };

        var joined = GpuMemoryUsage.Join(samples, new Dictionary<string, ulong> { [Nvidia] = 1, [Amd] = 2 });

        Assert.Equal([Nvidia], joined.Keys);
    }

    [Fact]
    public void Join_MissingMemoryReading_LeavesUsageNull() {
        var samples = new Dictionary<string, GpuAdapterSample> {
            [Nvidia] = new(10, NoEngines),
            [Amd] = new(20, NoEngines),
        };

        var joined = GpuMemoryUsage.Join(samples, new Dictionary<string, ulong> { [Nvidia] = 5 });

        Assert.Equal(5UL, joined[Nvidia].DedicatedUsedBytes);
        Assert.Null(joined[Amd].DedicatedUsedBytes);
    }
}
