using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Pins the wording that explains a GPU with no utilisation figure — the state a VM's paravirtual
/// adapter, Intel's i915 and the proprietary NVIDIA blob all land in.</summary>
public class GpuNoReadingNoteTests {
    private const uint Nvidia = 0x10DE;
    private const uint Vmware = 0x15AD;
    private const uint Intel = 0x8086;

    [Theory]
    [InlineData(Vmware)]
    [InlineData(Intel)]
    [InlineData(null)]
    public void For_ANonNvidiaAdapter_BlamesTheDriver(uint? vendorId) {
        Assert.Equal("This GPU's driver publishes no utilization figure.",
                     GpuNoReadingNote.For(vendorId, nvidiaMetricsEnabled: false));
    }

    /// <summary>The one vendor with an opt-in source: point at the setting that would fill the figure.</summary>
    [Fact]
    public void For_AnNvidiaAdapterWithTheSettingOff_PointsAtTheSetting() {
        Assert.Equal("Turn on \"NVIDIA GPU utilization\" in Settings to read this card.",
                     GpuNoReadingNote.For(Nvidia, nvidiaMetricsEnabled: false));
    }

    /// <summary>With the setting already on, advertising it would be wrong — nvidia-smi is the thing that
    /// came back empty.</summary>
    [Fact]
    public void For_AnNvidiaAdapterWithTheSettingOn_DoesNotAdvertiseIt() {
        var note = GpuNoReadingNote.For(Nvidia, nvidiaMetricsEnabled: true);

        Assert.Equal("nvidia-smi reported no utilization for this card.", note);
        Assert.DoesNotContain("Settings", note);
    }

    /// <summary>The non-NVIDIA wording must not shift with a flag that cannot apply to it.</summary>
    [Fact]
    public void For_ANonNvidiaAdapter_IgnoresTheNvidiaFlag() {
        Assert.Equal(GpuNoReadingNote.For(Vmware, nvidiaMetricsEnabled: false),
                     GpuNoReadingNote.For(Vmware, nvidiaMetricsEnabled: true));
    }
}
