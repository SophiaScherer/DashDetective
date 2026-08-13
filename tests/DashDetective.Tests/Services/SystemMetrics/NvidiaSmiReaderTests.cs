using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Toolkit;
using DashDetective.Tests.Fakes;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="NvidiaSmiReader"/>: the bus-id normalisation that is the whole join, the CSV
/// parse, and the cadence and retirement rules that keep a process launch off the sampling path. No
/// process is ever started — the launcher is the M12 seam, faked.</summary>
public class NvidiaSmiReaderTests {
    /// <summary>
    /// The trap this class exists to avoid. nvidia-smi writes an <b>eight-digit</b> domain where sysfs
    /// writes four, so a raw join matches nothing and every NVIDIA card silently stays blank — with the
    /// process still being spawned every 15 seconds to produce readings nobody can use.
    /// </summary>
    [Theory]
    [InlineData("00000000:01:00.0", "0000:01:00.0")]
    [InlineData("00000000:0A:00.0", "0000:0a:00.0")]
    [InlineData(" 00000000:01:00.0 ", "0000:01:00.0")]
    [InlineData("0000:01:00.0", "0000:01:00.0")]
    [InlineData("00010000:65:00.0", "0000:65:00.0")]
    [InlineData("0:01:00.0", "0000:01:00.0")]
    public void NormalizeBusId_MatchesTheSysfsForm(string raw, string expected) {
        Assert.Equal(expected, NvidiaSmiReader.NormalizeBusId(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    public void NormalizeBusId_Unparseable_IsEmpty(string raw) {
        Assert.Equal("", NvidiaSmiReader.NormalizeBusId(raw));
    }

    [Fact]
    public void Parse_ReadsBusIdAndUtilisationKeyedTheSysfsWay() {
        var readings = NvidiaSmiReader.Parse("00000000:01:00.0, 37\n00000000:65:00.0, 4\n");

        Assert.Equal(2, readings.Count);
        Assert.Equal(37, readings["0000:01:00.0"]);
        Assert.Equal(4, readings["0000:65:00.0"]);
    }

    /// <summary>nvidia-smi prints "[N/A]" for a GPU that cannot answer. Dropping the row keeps the card at
    /// "—" instead of showing it pinned at 0%.</summary>
    [Fact]
    public void Parse_DropsRowsWithNoNumber() {
        var readings = NvidiaSmiReader.Parse("00000000:01:00.0, [N/A]\n00000000:65:00.0, 12\n");

        Assert.Equal(["0000:65:00.0"], readings.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    [InlineData("Failed to initialize NVML: Driver/library version mismatch\n")]
    public void Parse_GarbageYieldsNoReadings(string output) {
        Assert.Empty(NvidiaSmiReader.Parse(output));
    }

    [Fact]
    public async Task RefreshIfDue_RunsTheExpectedQueryOnce_AndCachesIt() {
        var launcher = new FakeProcessLauncher {
            NextCapture = new ProcessCapture(0, "00000000:01:00.0, 42\n", "", false),
        };
        var reader = new NvidiaSmiReader(launcher);

        reader.RefreshIfDue();
        await WaitForCalls(launcher, 1);

        Assert.Equal("nvidia-smi", launcher.Single.FileName);
        Assert.Equal(
            ["--query-gpu=pci.bus_id,utilization.gpu", "--format=csv,noheader,nounits"],
            launcher.Single.Arguments);
        Assert.Equal(42, reader.Utilisation("0000:01:00.0"));
    }

    /// <summary>The sampling path calls this every tick; only the first may spawn anything. Without the
    /// cadence gate a 0.5 s refresh interval means two process launches a second.</summary>
    [Fact]
    public async Task RefreshIfDue_DoesNotRespawnWithinTheRefreshWindow() {
        var launcher = new FakeProcessLauncher {
            NextCapture = new ProcessCapture(0, "00000000:01:00.0, 42\n", "", false),
        };
        var reader = new NvidiaSmiReader(launcher);

        reader.RefreshIfDue();
        await WaitForCalls(launcher, 1);
        for (var i = 0; i < 20; i++)
            reader.RefreshIfDue();

        Assert.Single(launcher.Calls);
    }

    /// <summary>A missing binary throws rather than returning a code. Retrying it forever would be a spawn
    /// storm in slow motion, so the first failure retires the reader for the session.</summary>
    [Fact]
    public async Task RefreshIfDue_AMissingBinaryRetiresTheReader() {
        var launcher = new FakeProcessLauncher { ThrowOnCall = new InvalidOperationException("no such file") };
        var reader = new NvidiaSmiReader(launcher);

        reader.RefreshIfDue();
        await WaitForCalls(launcher, 1);
        for (var i = 0; i < 5; i++)
            reader.RefreshIfDue();

        Assert.Single(launcher.Calls);
        Assert.Null(reader.Utilisation("0000:01:00.0"));
    }

    [Fact]
    public async Task RefreshIfDue_ANonZeroExitKeepsTheReadingsEmpty() {
        var launcher = new FakeProcessLauncher {
            NextCapture = new ProcessCapture(9, "", "NVML unknown error", false),
        };
        var reader = new NvidiaSmiReader(launcher);

        reader.RefreshIfDue();
        await WaitForCalls(launcher, 1);

        Assert.Null(reader.Utilisation("0000:01:00.0"));
    }

    [Fact]
    public void Utilisation_BeforeAnyRun_IsNull() {
        Assert.Null(new NvidiaSmiReader(new FakeProcessLauncher()).Utilisation("0000:01:00.0"));
    }

    /// <summary>The refresh is deliberately fire-and-forget, so a test has to wait for it rather than
    /// awaiting a handle the production path never exposes.</summary>
    private static async Task WaitForCalls(FakeProcessLauncher launcher, int count) {
        for (var i = 0; i < 200 && launcher.Calls.Count < count; i++)
            await Task.Delay(5);
    }
}
