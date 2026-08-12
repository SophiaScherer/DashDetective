using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Toolkit;
using DashDetective.Tests.Fakes;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxGpuUsageSampler"/>: the amdgpu utilisation read, the deliberate absence
/// of both an engine breakdown and any reading for a driver that publishes none, and — the one that
/// matters most — that its keys line up with the adapter enumeration's.</summary>
public class LinuxGpuUsageSamplerTests {
    [Fact]
    public void SampleAdapters_ReadsGpuBusyPercentKeyedByPciAddress() {
        using var sampler = new LinuxGpuUsageSampler(new FakeProcFileSystem().WithAmdgpuCard());

        var (key, sample) = Assert.Single(sampler.SampleAdapters());

        Assert.Equal("0000:03:00.0", key);
        Assert.Equal(37, sample.Overall);
    }

    /// <summary>sysfs has no per-engine breakdown outside root-only debugfs. An empty map is what keeps the
    /// Performance tab's Detailed toggle hidden instead of opening an empty grid.</summary>
    [Fact]
    public void SampleAdapters_ReportsNoEngineBreakdown() {
        using var sampler = new LinuxGpuUsageSampler(new FakeProcFileSystem().WithAmdgpuCard());

        Assert.Empty(Assert.Single(sampler.SampleAdapters()).Value.Engines);
    }

    /// <summary>An idle GPU and one whose driver cannot report utilisation must not look alike — but the
    /// adapter is still named, because the inventory drops any GPU this sampler does not report and the
    /// card would vanish entirely.</summary>
    [Fact]
    public void SampleAdapters_ReportsCardsWithNoUtilisationAsUnknownNotIdle() {
        using var sampler = new LinuxGpuUsageSampler(new FakeProcFileSystem().WithNvidiaCard());

        var (key, sample) = Assert.Single(sampler.SampleAdapters());

        Assert.Equal("0000:01:00.0", key);
        Assert.Null(sample.Overall);
    }

    [Fact]
    public void SampleAdapters_MixedMachine_NamesBothCardsAndValuesOnlyTheReportingOne() {
        var proc = new FakeProcFileSystem().WithAmdgpuCard().WithNvidiaCard();
        using var sampler = new LinuxGpuUsageSampler(proc);

        var samples = sampler.SampleAdapters();

        Assert.Equal(["0000:01:00.0", "0000:03:00.0"], samples.Keys.Order());
        Assert.Equal(37, samples["0000:03:00.0"].Overall);
        Assert.Null(samples["0000:01:00.0"].Overall);
    }

    /// <summary>
    /// The invariant the whole GPU surface rests on. <c>DeviceInventory.Compose</c> keeps only the adapters
    /// present in <i>both</i> the enumeration and this sampler, so if the two derived their keys separately
    /// and disagreed, every GPU card would vanish with nothing logged and every individual reading still
    /// looking correct.
    /// </summary>
    [Fact]
    public async Task SampleAdapters_KeysMatchTheAdapterEnumerationsTokens() {
        var proc = new FakeProcFileSystem().WithAmdgpuCard().WithNvidiaCard();

        var adapters = await new LinuxGpuAdapterProvider(proc).GetAsync();
        using var sampler = new LinuxGpuUsageSampler(proc);
        var sampled = sampler.SampleAdapters().Keys.ToHashSet();

        var tokens = adapters.Select(a => a.LuidToken).ToHashSet();

        // Set equality, not merely a non-empty overlap: every enumerated adapter must survive the
        // inventory's intersection, including the one with no utilisation to report.
        Assert.Equal(tokens.Order(), sampled.Order());
    }

    /// <summary>The whole justification for the setting: with it off, nothing is ever launched. Asserted on
    /// a machine that <i>has</i> an NVIDIA card, since that is the only case where a spawn would be
    /// tempting.</summary>
    [Fact]
    public void SampleAdapters_NvidiaDisabled_NeverLaunchesAnything() {
        var launcher = new FakeProcessLauncher();
        using var sampler = new LinuxGpuUsageSampler(
            new FakeProcFileSystem().WithNvidiaCard(), new NvidiaSmiReader(launcher));

        for (var i = 0; i < 5; i++)
            _ = sampler.SampleAdapters();

        Assert.Empty(launcher.Calls);
        Assert.Null(Assert.Single(sampler.SampleAdapters()).Value.Overall);
    }

    /// <summary>An AMD-only box has nothing for nvidia-smi to say, so the opt-in must still not spawn
    /// it.</summary>
    [Fact]
    public void SampleAdapters_NvidiaEnabledButNoNvidiaCard_StillLaunchesNothing() {
        var launcher = new FakeProcessLauncher();
        using var sampler = new LinuxGpuUsageSampler(
            new FakeProcFileSystem().WithAmdgpuCard(), new NvidiaSmiReader(launcher)) {
            NvidiaMetricsEnabled = true,
        };

        _ = sampler.SampleAdapters();

        Assert.Empty(launcher.Calls);
    }

    [Fact]
    public async Task SampleAdapters_NvidiaEnabled_FillsTheNvidiaCardFromTheHelper() {
        var launcher = new FakeProcessLauncher {
            NextCapture = new ProcessCapture(0, "00000000:01:00.0, 61\n", "", false),
        };
        var proc = new FakeProcFileSystem().WithAmdgpuCard().WithNvidiaCard();
        using var sampler = new LinuxGpuUsageSampler(proc, new NvidiaSmiReader(launcher)) {
            NvidiaMetricsEnabled = true,
        };

        // The first tick starts the run and reports nothing for NVIDIA; a later one sees the result.
        _ = sampler.SampleAdapters();
        for (var i = 0; i < 200 && launcher.Calls.Count == 0; i++)
            await Task.Delay(5);

        var samples = sampler.SampleAdapters();

        Assert.Equal(61, samples["0000:01:00.0"].Overall);
        // sysfs still owns the AMD card — the helper never overrides a reading sysfs can give.
        Assert.Equal(37, samples["0000:03:00.0"].Overall);
    }

    [Theory]
    [InlineData("37\n", 37.0)]
    [InlineData("0\n", 0.0)]
    [InlineData("100", 100.0)]
    [InlineData("150\n", 100.0)]
    [InlineData("-5\n", 0.0)]
    public void ParsePercent_ClampsToTheDisplayRange(string text, double expected) {
        Assert.Equal(expected, LinuxGpuUsageSampler.ParsePercent(text));
    }

    /// <summary>A vanished or unreadable file drops the card from the tick; the view models leave such a
    /// card at its previous value rather than dropping it to zero.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("N/A\n")]
    public void ParsePercent_UnreadableIsNoReading(string? text) {
        Assert.Null(LinuxGpuUsageSampler.ParsePercent(text));
    }

    [Fact]
    public void SampleAdapters_NoDrmTree_ReturnsEmptyForever() {
        using var sampler = new LinuxGpuUsageSampler(new FakeProcFileSystem());

        Assert.Empty(sampler.SampleAdapters());
        Assert.Empty(sampler.SampleAdapters());
    }

    /// <summary>The real constructor reads the live filesystem; on a box with no <c>/sys</c> that must be
    /// an empty map rather than a throw.</summary>
    [Fact]
    public void RealFileSystem_SoftFailsToEmpty() {
        if (System.OperatingSystem.IsLinux())
            return;

        using var sampler = new LinuxGpuUsageSampler();

        Assert.Empty(sampler.SampleAdapters());
    }
}
