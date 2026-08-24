using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers the detail header's jump to the tab that owns the selected device. The page names a
/// device and raises what it is looking at; only the shell knows which tab that means, so what these pin
/// is that each row kind offers the right link and carries the right identity in it.</summary>
public class PerformanceJumpTests {
    private const int DiskNumber = 3;

    private static readonly IReadOnlyList<PhysicalDiskInfo> OneDisk = [
        new(DiskNumber, "Boot Drive", "NVMe SSD", 1_000_000_000_000, true),
    ];

    private static IReadOnlyList<VolumeInfo> Volumes() => [
        new(DiskNumber, SystemDrive.Letter, "", "NTFS", 1_000_000_000_000, 500_000_000_000),
    ];

    private static async Task<PerformanceViewModel> LoadedAsync() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");

        var viewModel = new PerformanceViewModel(
            new SystemMetricsService(samplers, () => new FakeUiTimer()),
            StubHardwareProviders.With(disks: OneDisk, volumes: Volumes()),
            () => new FakeGpuUsageSampler());

        // The constructor fires its own load and forgets it; letting that finish keeps the row set stable.
        await Task.Delay(100);
        await viewModel.LoadInventoryAsync();
        return viewModel;
    }

    private static ResourceRow Row(PerformanceViewModel page, string name) =>
        page.Resources.First(row => row.Name == name);

    [Fact]
    public async Task ADiskRow_JumpsToStorageCarryingItsDiskNumber() {
        var page = await LoadedAsync();
        var disk = page.Resources.First(row => row.Series == ChartSeries.Storage);
        int? asked = null;
        page.StorageRevealRequested += number => asked = number;

        disk.Link!.Command.Execute(null);

        Assert.Equal(DiskNumber, asked);
    }

    [Fact]
    public async Task ADiskRow_LabelsItsLinkForStorage() {
        var page = await LoadedAsync();
        var disk = page.Resources.First(row => row.Series == ChartSeries.Storage);

        Assert.Equal("View in Storage", disk.Link?.Label);
        Assert.True(disk.HasLink);
    }

    [Fact]
    public async Task TheNetworkRow_JumpsToNetworkCarryingTheAdapterName() {
        var page = await LoadedAsync();
        var network = page.Resources.First(row => row.Series == ChartSeries.NetDown);
        string? asked = null;
        page.NetworkRevealRequested += name => asked = name;

        network.Link!.Command.Execute(null);

        Assert.Equal(network.Name, asked);
    }

    [Theory]
    [InlineData("CPU")]
    [InlineData("Memory")]
    public async Task TheCpuAndMemoryRows_JumpToHardware(string rowName) {
        var page = await LoadedAsync();
        var row = Row(page, rowName);
        var raised = 0;
        page.HardwareRevealRequested += () => raised++;

        Assert.Equal("View in Hardware", row.Link?.Label);
        row.Link!.Command.Execute(null);

        Assert.Equal(1, raised);
    }

    // Every row the rail can show offers somewhere to go, so the link is never a dead affordance.
    [Fact]
    public async Task EveryRailRow_HasALink() {
        var page = await LoadedAsync();

        Assert.NotEmpty(page.Resources);
        Assert.All(page.Resources, row => Assert.True(row.HasLink, $"{row.Name} has no link"));
    }
}
