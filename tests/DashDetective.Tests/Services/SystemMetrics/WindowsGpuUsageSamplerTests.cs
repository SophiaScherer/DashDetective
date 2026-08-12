using DashDetective.Services.SystemMetrics;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="WindowsGpuUsageSampler"/>'s pure aggregation helpers — the per-adapter LUID
/// split that backs multi-GPU support. The PDH marshalling path is verified by smoke-run, not
/// unit-tested.</summary>
public class WindowsGpuUsageSamplerTests {
    private const string Nvidia = "luid_0x00000000_0x0000e54b";
    private const string Amd = "luid_0x00000000_0x0000f83d";

    [Fact]
    public void ParseLuidToken_ExtractsAndLowercasesTheAdapterToken() {
        Assert.Equal(Nvidia,
            WindowsGpuUsageSampler.ParseLuidToken("pid_1234_luid_0x00000000_0x0000E54B_phys_0_eng_0_engtype_3D"));
    }

    [Fact]
    public void ParseLuidToken_ReturnsNullWhenNoLuidPresent() {
        Assert.Null(WindowsGpuUsageSampler.ParseLuidToken("pid_1234_phys_0_eng_0_engtype_3D"));
        Assert.Null(WindowsGpuUsageSampler.ParseLuidToken(""));
        Assert.Null(WindowsGpuUsageSampler.ParseLuidToken(null));
    }

    [Fact]
    public void AggregateAdapters_GroupsByLuid_SumsEngines_AndTakesBusiestAsOverall() {
        var items = new (string?, double)[] {
            ("pid_1000_luid_0x00000000_0x0000E54B_phys_0_eng_0_engtype_3D", 30),
            ("pid_2000_luid_0x00000000_0x0000E54B_phys_0_eng_1_engtype_3D", 40),
            ("pid_1000_luid_0x00000000_0x0000E54B_phys_0_eng_2_engtype_Copy", 10),
            ("pid_3000_luid_0x00000000_0x0000F83D_phys_0_eng_0_engtype_VideoDecode", 55),
        };

        var result = WindowsGpuUsageSampler.AggregateAdapters(items);

        Assert.Equal(2, result.Count);
        // NVIDIA: 3D sums to 70 across its two process instances (the busiest engine → Overall), Copy 10.
        Assert.Equal(70, result[Nvidia].Overall);
        Assert.Equal(70, result[Nvidia].Engines["3D"]);
        Assert.Equal(10, result[Nvidia].Engines["Copy"]);
        // AMD: a single VideoDecode reading.
        Assert.Equal(55, result[Amd].Overall);
        Assert.Equal(55, result[Amd].Engines["VideoDecode"]);
    }

    /// <summary><c>Overall</c> is nullable so Linux can say "this adapter exists but publishes no
    /// utilisation". PDH always produces a figure, so a null here would blank a Windows GPU card that used
    /// to show a number.</summary>
    [Fact]
    public void AggregateAdapters_AlwaysFillsOverall() {
        var items = new (string?, double)[] {
            ("pid_1000_luid_0x00000000_0x0000E54B_phys_0_eng_0_engtype_3D", 0),
        };

        Assert.NotNull(WindowsGpuUsageSampler.AggregateAdapters(items)[Nvidia].Overall);
    }

    [Fact]
    public void AggregateAdapters_ClampsOverallTo100ButKeepsRawEngineSums() {
        var items = new (string?, double)[] {
            ("pid_1_luid_0x00000000_0x0000E54B_phys_0_eng_0_engtype_3D", 80),
            ("pid_2_luid_0x00000000_0x0000E54B_phys_0_eng_1_engtype_3D", 50),
        };

        var result = WindowsGpuUsageSampler.AggregateAdapters(items);

        Assert.Equal(100, result[Nvidia].Overall);   // 130 clamped for display
        Assert.Equal(130, result[Nvidia].Engines["3D"]); // raw sum preserved
    }

    [Fact]
    public void AggregateAdapters_SkipsInstancesMissingLuidOrEngineToken() {
        var items = new (string?, double)[] {
            ("pid_1_phys_0_eng_0_engtype_3D", 99),                                   // no luid
            ("pid_2_luid_0x00000000_0x0000E54B_phys_0_eng_0", 99),                    // no engtype
            (null, 99),
            ("pid_3_luid_0x00000000_0x0000E54B_phys_0_eng_0_engtype_3D", 25),         // the only valid one
        };

        var result = WindowsGpuUsageSampler.AggregateAdapters(items);

        Assert.Single(result);
        Assert.Equal(25, result[Nvidia].Overall);
    }
}
