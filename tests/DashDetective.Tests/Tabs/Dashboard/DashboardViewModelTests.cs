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
    private static DashboardViewModel Create(HardwareProviders providers) {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        return new DashboardViewModel(new SystemMetricsService(samplers, () => new FakeUiTimer()), providers);
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
