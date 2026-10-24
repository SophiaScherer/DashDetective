using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>
/// Pins Task Manager's GPU rule in <see cref="GpuEngineLoad"/>: an engine's load is the sum of the process
/// instances on that one physical engine, an adapter reads its busiest engine, a type reads its busiest
/// engine of that type, and a process reads its busiest engine. Engines of one type are never summed.
/// </summary>
public class GpuEngineLoadTests {
    private const string Nvidia = "luid_0x00000000_0x0000e7be";
    private const string Amd = "luid_0x00000000_0x0000f83d";

    [Fact]
    public void ByAdapter_TwoProcessesOnOneEngine_Sum() {
        var readings = new (string?, double)[] {
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 20),
            ("pid_200_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 25),
        };

        var adapter = GpuEngineLoad.ByAdapter(readings)[Nvidia];

        Assert.Equal(45, adapter.Engines["3D"]);
        Assert.Equal(45, adapter.Overall);
    }

    /// <summary>The RTX 3060's six Copy engines, each lightly busy. Summed by type they read 62 %, which no
    /// engine was doing and which then outranked 3D for the adapter's headline.</summary>
    [Fact]
    public void ByAdapter_SixCopyEngines_ReadTheBusiestNotTheSum() {
        var readings = new (string?, double)[] {
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 30),
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_3_engtype_Copy", 10),
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_4_engtype_Copy", 10),
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_9_engtype_Copy", 10),
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_10_engtype_Copy", 12),
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_11_engtype_Copy", 10),
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_12_engtype_Copy", 10),
        };

        var adapter = GpuEngineLoad.ByAdapter(readings)[Nvidia];

        Assert.Equal(12, adapter.Engines["Copy"]);
        Assert.Equal(30, adapter.Overall);
    }

    [Fact]
    public void ByAdapter_Overall_IsTheBusiestEngine() {
        var readings = new (string?, double)[] {
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 15),
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_5_engtype_Video Codec 0", 64),
            ("pid_200_luid_0x00000000_0x0000E7BE_phys_0_eng_3_engtype_Copy", 8),
        };

        var adapter = GpuEngineLoad.ByAdapter(readings)[Nvidia];

        Assert.Equal(64, adapter.Overall);
        Assert.Equal(15, adapter.Engines["3D"]);
        Assert.Equal(64, adapter.Engines["Video Codec 0"]);
        Assert.Equal(8, adapter.Engines["Copy"]);
    }

    /// <summary>Same engine index and type on two adapters are two engines on two cards.</summary>
    [Fact]
    public void ByAdapter_TwoAdapters_StaySeparate() {
        var readings = new (string?, double)[] {
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 50),
            ("pid_100_luid_0x00000000_0x0000F83D_phys_0_eng_0_engtype_3D", 20),
        };

        var result = GpuEngineLoad.ByAdapter(readings);

        Assert.Equal(2, result.Count);
        Assert.Equal(50, result[Nvidia].Overall);
        Assert.Equal(20, result[Amd].Overall);
    }

    /// <summary>An overlapping sum can pass 100 and a counter wrap can read negative; both clamp per engine,
    /// before anything is compared.</summary>
    [Fact]
    public void ByAdapter_ClampsEachEngineTo0And100() {
        var readings = new (string?, double)[] {
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 70),
            ("pid_200_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 60),
            ("pid_100_luid_0x00000000_0x0000F83D_phys_0_eng_0_engtype_3D", -5),
        };

        var result = GpuEngineLoad.ByAdapter(readings);

        Assert.Equal(100, result[Nvidia].Overall);
        Assert.Equal(100, result[Nvidia].Engines["3D"]);
        Assert.Equal(0, result[Amd].Overall);
        Assert.Equal(0, result[Amd].Engines["3D"]);
    }

    [Fact]
    public void ByAdapter_UnparsableNames_AreSkipped() {
        var readings = new (string?, double)[] {
            (null, 99),
            ("_Total", 99),
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_0", 99),
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 25),
        };

        var result = GpuEngineLoad.ByAdapter(readings);

        Assert.Single(result);
        Assert.Equal(25, result[Nvidia].Overall);
    }

    /// <summary>One process drawing on 3D on both cards reads its busier card, not their sum.</summary>
    [Fact]
    public void ByProcess_OnePidOn3DAcrossTwoAdapters_ReadsTheMax() {
        var readings = new (string?, double)[] {
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 35),
            ("pid_100_luid_0x00000000_0x0000F83D_phys_0_eng_0_engtype_3D", 25),
        };

        Assert.Equal(35, GpuEngineLoad.ByProcess(readings)[100]);
    }

    [Fact]
    public void ByProcess_OnePidOnTwoCopyEngines_ReadsTheMax() {
        var readings = new (string?, double)[] {
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_3_engtype_Copy", 12),
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_4_engtype_Copy", 9),
        };

        Assert.Equal(12, GpuEngineLoad.ByProcess(readings)[100]);
    }

    /// <summary>A process's figure is its own share of an engine, summed over duplicate instances of that
    /// engine and clamped; an instance with no PID belongs to no row.</summary>
    [Fact]
    public void ByProcess_SumsDuplicateInstancesOfOneEngine_AndSkipsInstancesWithoutAPid() {
        var readings = new (string?, double)[] {
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 60),
            ("pid_100_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 55),
            ("pid_200_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 5),
            ("luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D", 90),
        };

        var result = GpuEngineLoad.ByProcess(readings);

        Assert.Equal(2, result.Count);
        Assert.Equal(100, result[100]);
        Assert.Equal(5, result[200]);
    }
}
