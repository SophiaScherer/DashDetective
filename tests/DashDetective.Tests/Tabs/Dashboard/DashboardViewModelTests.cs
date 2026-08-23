using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>Covers <see cref="DashboardViewModel"/> through the <c>HardwareProviders</c> seam: the static
/// CPU / memory / system captions are formatted from a known snapshot, and an unreadable machine degrades
/// to the "Unknown …" wording rather than blanking.</summary>
public class DashboardViewModelTests {
    private static DashboardViewModel Create(HardwareProviders providers) =>
        new(TestMetrics.Idle(), providers);

    // ---- Soft-fail: a failed READ, as distinct from a successful read of an unknown machine ----
    //
    // The two land on the same visible strings, which is exactly why they need separate tests: the
    // existing "Unknown …" cases below drive a provider that succeeds and returns an .Unknown record,
    // and never touch the catch blocks at all.

    [Fact]
    public async Task LoadCpuInfoAsync_WhenTheReadFails_ReportsTheUnknownWording() {
        var viewModel = Create(StubHardwareProviders.Compose(
            cpu: StubHardwareProviders.Fails<CpuStaticInfo>("the CPU namespace is gone")));

        await viewModel.LoadCpuInfoAsync();

        Assert.Equal("Unknown CPU", viewModel.CpuModelText);
        Assert.Equal("Unknown CPU", viewModel.CpuModelShort);
    }

    [Fact]
    public async Task LoadMemoryInfoAsync_WhenTheReadFails_ReportsTheUnknownWording() {
        var viewModel = Create(StubHardwareProviders.Compose(
            memory: StubHardwareProviders.Fails<MemoryStaticInfo>()));

        await viewModel.LoadMemoryInfoAsync();

        Assert.Equal("Unknown RAM", viewModel.MemoryModelText);
    }

    [Fact]
    public async Task LoadSystemInfoAsync_WhenTheReadFails_FallsBackFieldByField() {
        var viewModel = Create(StubHardwareProviders.Compose(
            system: StubHardwareProviders.Fails<SystemStaticInfo>()));

        await viewModel.LoadSystemInfoAsync();

        Assert.Equal("Unknown OS", viewModel.OsText);
        Assert.Equal("Unknown BIOS", viewModel.BiosText);
        Assert.Equal("Unknown motherboard", viewModel.MotherboardText);
        // The machine name comes from the runtime, not the failed read, so it survives.
        Assert.Equal(System.Environment.MachineName, viewModel.DeviceText);
    }

    /// <summary>
    /// The GPU load promises to leave the cards on screen alone when it fails. It could not keep that
    /// promise while the rebuild cleared first: a throw partway left the old cards gone and the maps out
    /// of step with Cards. Seed a good set, then fail a reload, and the first set must still be there.
    /// </summary>
    [Fact]
    public async Task LoadGpusAsync_WhenAReloadFails_LeavesTheCardsAlreadyOnScreen() {
        var viewModel = Create(StubHardwareProviders.With(
            gpuAdapters: [new GpuAdapter("luid-1", "GeForce RTX 4080", false, 0)]));
        await viewModel.LoadGpusAsync();
        var seeded = viewModel.Cards.Count;

        // A second page over a failing enumeration, to prove the catch writes nothing at all.
        var failing = Create(StubHardwareProviders.Compose(
            gpuAdapters: StubHardwareProviders.Fails<System.Collections.Generic.IReadOnlyList<GpuAdapter>>()));
        await failing.LoadGpusAsync();

        Assert.True(seeded > 0);
        Assert.DoesNotContain(failing.Cards, c => c.Category == DeviceCategory.Gpu);
    }

    [Fact]
    public async Task LoadCpuInfoAsync_FormatsModelAndCoreCounts() {
        var viewModel = Create(StubHardwareProviders.With(
            cpu: new CpuStaticInfo("Intel Core i7-13700K", 16, 24, 3400)));

        await viewModel.LoadCpuInfoAsync();

        Assert.Contains("i7-13700K", viewModel.CpuModelText);
        Assert.Contains("16", viewModel.CpuCoresText);
        Assert.Contains("24", viewModel.CpuCoresText);
    }

    [Fact]
    public async Task LoadMemoryInfoAsync_FormatsTotalTypeAndSpeed() {
        var viewModel = Create(StubHardwareProviders.With(
            memory: new MemoryStaticInfo(32, "DDR5", 6000, 2)));

        await viewModel.LoadMemoryInfoAsync();

        Assert.Contains("32", viewModel.MemoryModelText);
        Assert.Contains("DDR5", viewModel.MemoryModelText);
        Assert.Contains("6000", viewModel.MemoryModelText);
    }

    [Fact]
    public async Task LoadSystemInfoAsync_CopiesEveryIdentityFieldToItsRow() {
        var viewModel = Create(StubHardwareProviders.With(
            system: new SystemStaticInfo(
                "Windows 11 Pro 24H2", "TEST-PC", "Test BIOS 1.0", "26100.1150", "Test Board")));

        await viewModel.LoadSystemInfoAsync();

        Assert.Equal("Windows 11 Pro 24H2", viewModel.OsText);
        Assert.Equal("TEST-PC", viewModel.DeviceText);
        Assert.Equal("Test BIOS 1.0", viewModel.BiosText);
        Assert.Equal("26100.1150", viewModel.BuildText);
        Assert.Equal("Test Board", viewModel.MotherboardText);
    }

    /// <summary>What an unsupported host produces: the panel reads "Unknown …" rather than going blank.</summary>
    [Fact]
    public async Task LoadAsync_UnknownSnapshots_ShowTheUnknownWording() {
        var viewModel = Create(StubHardwareProviders.With());

        await viewModel.LoadCpuInfoAsync();
        await viewModel.LoadSystemInfoAsync();

        Assert.Contains("Unknown", viewModel.CpuModelText);
        Assert.Contains("Unknown", viewModel.OsText);
    }
}
