using DashDetective.Tabs.Processes;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>
/// Covers <see cref="GpuEngineInstanceName"/>, the pure half of the per-process GPU sampler, which
/// is otherwise a PDH query. Windows names each <c>GPU Engine</c> counter instance after the process, the
/// adapter and the engine, and the whole per-process GPU column depends on pulling the PID and the engine
/// type back out of that string.
/// </summary>
public class GpuEngineInstanceNameTests {
    /// <summary>The real shape, as PDH reports it on a machine with one adapter.</summary>
    [Fact]
    public void TryParse_RealInstanceName_YieldsThePidAndEngineType() {
        var ok = GpuEngineInstanceName.TryParse(
            "pid_1234_luid_0x00000000_0x0000e54b_phys_0_eng_0_engtype_3D", out var pid, out var engine);

        Assert.True(ok);
        Assert.Equal(1234, pid);
        Assert.Equal("3D", engine);
    }

    [Theory]
    [InlineData("pid_4_luid_0x00000000_0x0000e54b_phys_0_eng_1_engtype_Copy", 4, "Copy")]
    [InlineData("pid_65535_luid_0x0_0x1_phys_0_eng_2_engtype_VideoDecode", 65535, "VideoDecode")]
    [InlineData("pid_900_luid_0x0_0x1_phys_1_eng_3_engtype_VideoProcessing", 900, "VideoProcessing")]
    public void TryParse_EachEngineType_IsCarriedThrough(string instance, int expectedPid, string expectedEngine) {
        Assert.True(GpuEngineInstanceName.TryParse(instance, out var pid, out var engine));
        Assert.Equal(expectedPid, pid);
        Assert.Equal(expectedEngine, engine);
    }

    /// <summary>The engine token is found from the END of the string. A LUID can itself contain the
    /// substring, so scanning forwards would slice the engine name out of the middle of the adapter id.</summary>
    [Fact]
    public void TryParse_TakesTheLastEngineToken() {
        Assert.True(GpuEngineInstanceName.TryParse(
            "pid_7_luid_0x0_0xengtype_beef_phys_0_eng_0_engtype_3D", out _, out var engine));

        Assert.Equal("3D", engine);
    }

    /// <summary>An instance with no engine suffix still yields its PID: the sampler groups by (pid,
    /// engine) and picks the busiest engine, so an unnamed engine is a real bucket, not a parse failure.</summary>
    [Fact]
    public void TryParse_NoEngineToken_StillYieldsThePidWithAnEmptyEngine() {
        Assert.True(GpuEngineInstanceName.TryParse("pid_42_luid_0x0_0x1_phys_0", out var pid, out var engine));

        Assert.Equal(42, pid);
        Assert.Equal("", engine);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("luid_0x00000000_0x0000e54b_phys_0_eng_0_engtype_3D")]  // no pid_ token at all
    [InlineData("pid_notanumber_engtype_3D")]                            // pid_ present, no digits
    [InlineData("pid_")]                                                 // truncated
    public void TryParse_UnusableInstanceName_IsRejectedRatherThanGuessed(string? instance) {
        Assert.False(GpuEngineInstanceName.TryParse(instance, out var pid, out _));
        Assert.Equal(0, pid);
    }

    /// <summary>A PID wider than <see cref="int"/> must be rejected, not wrapped: a negative or truncated
    /// PID would attribute one process's GPU time to an unrelated row.</summary>
    [Fact]
    public void TryParse_PidTooLargeForAnInt_IsRejected() {
        Assert.False(GpuEngineInstanceName.TryParse("pid_99999999999_engtype_3D", out var pid, out _));
        Assert.Equal(0, pid);
    }
}
