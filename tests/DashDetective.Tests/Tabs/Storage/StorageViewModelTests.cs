using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Tabs.Storage;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Storage;

/// <summary>Covers <see cref="StorageViewModel"/> through the <c>HardwareProviders</c> seam: partition
/// ordering, and <c>SelectDefaultDrive</c>'s rule that a reload keeps the drive the user was watching and
/// otherwise falls to the system disk.</summary>
public class StorageViewModelTests {
    private const int SystemDiskNumber = 1;
    private const int OtherDiskNumber = 0;

    private static readonly IReadOnlyList<PhysicalDiskInfo> TwoDisks = [
        new(OtherDiskNumber, "Data Drive", "HDD", 2_000_000_000_000, true),
        new(SystemDiskNumber, "Boot Drive", "NVMe SSD", 1_000_000_000_000, true),
    ];

    /// <summary>One lettered volume per disk, with the system drive letter on <see cref="SystemDiskNumber"/>,
    /// plus an unlettered Recovery partition to pin the ordering rule.</summary>
    private static IReadOnlyList<VolumeInfo> Volumes() => [
        new(OtherDiskNumber, 'D', "Data", "NTFS", 2_000_000_000_000, 1_000_000_000_000),
        new(SystemDiskNumber, SystemDrive.Letter, "", "NTFS", 1_000_000_000_000, 500_000_000_000),
        new(SystemDiskNumber, null, "Recovery", "NTFS", 800_000_000, 100_000_000),
    ];

    private static async Task<StorageViewModel> LoadedAsync(
        IReadOnlyList<PhysicalDiskInfo>? disks = null, IReadOnlyList<VolumeInfo>? volumes = null) {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var providers = StubHardwareProviders.With(disks: disks ?? TwoDisks, volumes: volumes ?? Volumes());

        var viewModel = new StorageViewModel(metrics, providers);
        await viewModel.LoadStorageAsync();
        return viewModel;
    }

    [Fact]
    public async Task LoadStorageAsync_BuildsOneCardPerPhysicalDisk() {
        var viewModel = await LoadedAsync();

        Assert.Equal(2, viewModel.Drives.Count);
        Assert.True(viewModel.HasMultipleDrives);
    }

    /// <summary>Lettered volumes come first, in letter order; the unlettered Recovery/EFI partitions the
    /// comp shows sort to the end rather than being dropped.</summary>
    [Fact]
    public async Task LoadStorageAsync_OrdersLetteredPartitionsFirst() {
        var viewModel = await LoadedAsync();

        var letters = viewModel.Partitions.Select(p => p.Vol).ToList();
        Assert.Equal(3, letters.Count);
        Assert.Equal($"{SystemDrive.Letter}:", letters[0]);
        Assert.Equal("D:", letters[1]);
        Assert.DoesNotContain(':', letters[2]);
    }

    /// <summary>With nothing previously selected, the panel opens on the drive hosting Windows — the drive
    /// the page names in its title.</summary>
    [Fact]
    public async Task LoadStorageAsync_FirstLoad_SelectsTheSystemDisk() {
        var viewModel = await LoadedAsync();

        Assert.Equal("Boot Drive", viewModel.SelectedDrive?.Model);
    }

    /// <summary>A toolbar Refresh rebuilds every card, and must not silently move the Disk Activity panel
    /// off the drive being watched.</summary>
    [Fact]
    public async Task LoadStorageAsync_Reload_KeepsTheDriveTheUserSelected() {
        var viewModel = await LoadedAsync();
        viewModel.Drives.Single(d => d.Model == "Data Drive").SelectCommand.Execute(null);

        await viewModel.LoadStorageAsync();

        Assert.Equal("Data Drive", viewModel.SelectedDrive?.Model);
    }

    /// <summary>When neither a previous nor a system disk resolves, the first card wins rather than the
    /// panel being left empty.</summary>
    [Fact]
    public async Task LoadStorageAsync_NoSystemVolume_FallsBackToTheFirstCard() {
        var viewModel = await LoadedAsync(volumes: [
            new(OtherDiskNumber, 'D', "Data", "NTFS", 2_000_000_000_000, 1_000_000_000_000),
        ]);

        Assert.NotNull(viewModel.SelectedDrive);
        Assert.Same(viewModel.Drives[0], viewModel.SelectedDrive);
    }
}
