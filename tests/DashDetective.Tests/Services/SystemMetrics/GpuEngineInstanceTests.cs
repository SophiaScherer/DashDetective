using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>
/// Covers <see cref="GpuEngineInstance.TryParse"/>, which names the one physical engine a
/// <c>\GPU Engine(*)</c> counter instance measures. Every GPU figure in the app is grouped by what it returns,
/// so a misread index merges two engines and a misread LUID drops the adapter from the inventory join.
/// </summary>
public class GpuEngineInstanceTests {
    private const string Nvidia = "luid_0x00000000_0x0000e7be";

    /// <summary>A real instance from an RTX 3060, as PDH spells it.</summary>
    [Fact]
    public void TryParse_RealInstanceName_YieldsEveryField() {
        Assert.True(GpuEngineInstance.TryParse(
            "pid_1234_luid_0x00000000_0x0000E7BE_phys_0_eng_3_engtype_Copy", out var instance));

        Assert.Equal(new GpuEngineInstance(1234, Nvidia, 0, 3, "Copy"), instance);
    }

    /// <summary>Types carry spaces, digits and underscores; the index inside "Video Codec 0" or "OFA_0" is
    /// part of the type, not the engine number.</summary>
    [Theory]
    [InlineData("pid_4_luid_0x00000000_0x0000e7be_phys_0_eng_5_engtype_Video Codec 0", 5, "Video Codec 0")]
    [InlineData("pid_4_luid_0x00000000_0x0000e7be_phys_0_eng_1_engtype_High Priority 3D", 1, "High Priority 3D")]
    [InlineData("pid_4_luid_0x00000000_0x0000e7be_phys_0_eng_7_engtype_Compute 1", 7, "Compute 1")]
    [InlineData("pid_4_luid_0x00000000_0x0000e7be_phys_0_eng_14_engtype_OFA_0", 14, "OFA_0")]
    [InlineData("pid_4_luid_0x00000000_0x0000e7be_phys_0_eng_0_engtype_3D", 0, "3D")]
    [InlineData("pid_65535_luid_0x0_0x1_phys_0_eng_2_engtype_VideoDecode", 2, "VideoDecode")]
    [InlineData("pid_900_luid_0x0_0x1_phys_1_eng_3_engtype_VideoProcessing", 3, "VideoProcessing")]
    public void TryParse_MultiWordAndNumberedTypes_AreCarriedWhole(string name, int engine, string type) {
        Assert.True(GpuEngineInstance.TryParse(name, out var instance));

        Assert.Equal(engine, instance.Engine);
        Assert.Equal(type, instance.Type);
    }

    /// <summary>The LUID is re-formatted exactly as <see cref="GpuAdapter.FormatLuidToken"/> writes it, so it
    /// joins the DXGI inventory whatever casing or width PDH used.</summary>
    [Theory]
    [InlineData("pid_1_luid_0x00000000_0x0000E7BE_phys_0_eng_0_engtype_3D")]
    [InlineData("pid_1_luid_0x00000000_0x0000e7be_phys_0_eng_0_engtype_3D")]
    [InlineData("pid_1_LUID_0X0_0XE7BE_phys_0_eng_0_engtype_3D")]
    public void TryParse_LuidOfAnyCasing_NormalizesToTheInventoryToken(string name) {
        Assert.True(GpuEngineInstance.TryParse(name, out var instance));

        Assert.Equal(GpuAdapter.FormatLuidToken(0, 0xE7BE), instance.Luid);
    }

    [Fact]
    public void TryParse_HighLuidHalf_IsCarried() {
        Assert.True(GpuEngineInstance.TryParse(
            "pid_1_luid_0x00000001_0x0000E7BE_phys_2_eng_0_engtype_3D", out var instance));

        Assert.Equal("luid_0x00000001_0x0000e7be", instance.Luid);
        Assert.Equal(2, instance.Phys);
    }

    /// <summary>The PID is the one optional field: an engine with no owner still has a load.</summary>
    [Fact]
    public void TryParse_NoPid_YieldsANullPid() {
        Assert.True(GpuEngineInstance.TryParse(
            "luid_0x00000000_0x0000E7BE_phys_0_eng_3_engtype_Copy", out var instance));

        Assert.Null(instance.Pid);
        Assert.Equal(new GpuEngineInstance(null, Nvidia, 0, 3, "Copy"), instance);
    }

    /// <summary>"_eng_" and "_engtype_" share a prefix; the index must come from the first and the type from
    /// the second.</summary>
    [Fact]
    public void TryParse_EngineIndexIsNotConfusedWithTheTypeToken() {
        Assert.True(GpuEngineInstance.TryParse(
            "pid_7_luid_0x0_0xbeef_phys_0_eng_2_engtype_3D", out var instance));

        Assert.Equal(2, instance.Engine);
        Assert.Equal("3D", instance.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("_Total")]
    [InlineData("pid_1_phys_0_eng_0_engtype_3D")]                                  // no luid
    [InlineData("pid_1_luid_0x00000000_0x0000e7be_eng_0_engtype_3D")]              // no phys
    [InlineData("pid_1_luid_0x00000000_0x0000e7be_phys_0_engtype_3D")]             // no eng
    [InlineData("pid_1_luid_0x00000000_0x0000e7be_phys_0_eng_0")]                  // no type
    [InlineData("pid_1_luid_0x00000000_0x0000e7be_phys_0_eng_0_engtype_")]         // empty type
    [InlineData("pid_1_luid_0x00000000_0x0000e7be_phys_0_eng_x_engtype_3D")]       // non-numeric eng
    [InlineData("pid_1_luid_0x00000000_0x0000e7be_phys__eng_0_engtype_3D")]        // empty phys
    [InlineData("pid_1_luid_0x0000000g_0x0000e7be_phys_0_eng_0_engtype_3D")]       // non-hex luid
    [InlineData("pid_1_luid_0x00000000_phys_0_eng_0_engtype_3D")]                  // half a luid
    [InlineData("pid_1_luid_0x100000000_0x0000e7be_phys_0_eng_0_engtype_3D")]      // luid half too wide
    [InlineData("pid_notanumber_luid_0x00000000_0x0000e7be_phys_0_eng_0_engtype_3D")]
    [InlineData("pid__luid_0x00000000_0x0000e7be_phys_0_eng_0_engtype_3D")]        // empty pid
    [InlineData("pid_")]                                                            // truncated
    [InlineData("pid_-1_luid_0x00000000_0x0000e7be_phys_0_eng_0_engtype_3D")]      // signed pid
    [InlineData("xyz_1_luid_0x00000000_0x0000e7be_phys_0_eng_0_engtype_3D")]       // unknown prefix
    public void TryParse_MalformedName_IsRejected(string? name) {
        Assert.False(GpuEngineInstance.TryParse(name, out var instance));
        Assert.Equal(default, instance);
    }

    /// <summary>A PID wider than <see cref="int"/> is rejected, not wrapped: a truncated PID would attribute
    /// one process's GPU time to an unrelated row.</summary>
    [Fact]
    public void TryParse_PidTooLargeForAnInt_IsRejected() {
        Assert.False(GpuEngineInstance.TryParse(
            "pid_99999999999_luid_0x00000000_0x0000e7be_phys_0_eng_0_engtype_3D", out _));
    }
}
