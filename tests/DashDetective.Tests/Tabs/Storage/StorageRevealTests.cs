using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Tabs.Storage;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Storage;

/// <summary>Covers <see cref="StorageViewModel.Reveal"/> — the seam a Performance disk row jumps through.
/// The awkward case is timing: the cards load asynchronously from the constructor, so a reveal can arrive
/// before there is anything to select and has to be held until there is.</summary>
public class StorageRevealTests {
    private const int SystemDiskNumber = 1;
    private const int OtherDiskNumber = 0;

    private static readonly IReadOnlyList<PhysicalDiskInfo> TwoDisks = [
        new(OtherDiskNumber, "Data Drive", "HDD", 2_000_000_000_000, true),
        new(SystemDiskNumber, "Boot Drive", "NVMe SSD", 1_000_000_000_000, true),
    ];

    private static IReadOnlyList<VolumeInfo> Volumes() => [
        new(OtherDiskNumber, 'D', "Data", "NTFS", 2_000_000_000_000, 1_000_000_000_000),
        new(SystemDiskNumber, SystemDrive.Letter, "", "NTFS", 1_000_000_000_000, 500_000_000_000),
    ];

    private static async Task<StorageViewModel> LoadedAsync() {
        var viewModel = new StorageViewModel(
            TestMetrics.Idle(), StubHardwareProviders.With(disks: TwoDisks, volumes: Volumes()));
        await viewModel.LoadStorageAsync();
        return viewModel;
    }

    /// <summary>A page whose disk read has not returned yet, so the constructor's fire-and-forget load is
    /// still in flight and there are genuinely no cards. Releasing the gate completes it. Plain stub values
    /// return already-completed tasks, which would let the ctor load finish before a test could act.</summary>
    private static (StorageViewModel Page, Action Release) Gated() {
        var gate = new TaskCompletionSource<IReadOnlyList<PhysicalDiskInfo>>();
        var page = new StorageViewModel(
            TestMetrics.Idle(),
            StubHardwareProviders.Compose(
                disks: () => gate.Task,
                volumes: () => Task.FromResult(Volumes())));
        return (page, () => gate.SetResult(TwoDisks));
    }

    [Fact]
    public async Task Reveal_SelectsTheNamedDrive() {
        var viewModel = await LoadedAsync();

        viewModel.Reveal(OtherDiskNumber);

        Assert.Equal(OtherDiskNumber, viewModel.SelectedDrive?.DiskNumber);
    }

    [Fact]
    public async Task Reveal_AsksTheViewToShowTheCard() {
        var viewModel = await LoadedAsync();
        var raised = 0;
        viewModel.RevealRequested += () => raised++;

        viewModel.Reveal(OtherDiskNumber);

        Assert.Equal(1, raised);
    }

    // The load is fired and forgotten from the constructor, so a jump can easily beat it.
    [Fact]
    public async Task Reveal_ArrivingBeforeTheLoad_IsHonoredOnceTheCardsExist() {
        var (page, release) = Gated();
        var raised = 0;
        page.RevealRequested += () => raised++;

        page.Reveal(OtherDiskNumber);
        Assert.Null(page.SelectedDrive);
        Assert.Equal(0, raised);

        release();
        await page.LoadStorageAsync();

        Assert.Equal(OtherDiskNumber, page.SelectedDrive?.DiskNumber);
        Assert.Equal(1, raised);
    }

    // A pending reveal outranks the system-disk default, which is what the load would otherwise pick.
    [Fact]
    public async Task APendingReveal_BeatsTheSystemDiskDefault() {
        var (page, release) = Gated();

        page.Reveal(OtherDiskNumber);
        release();
        await page.LoadStorageAsync();

        Assert.Equal(OtherDiskNumber, page.SelectedDrive?.DiskNumber);
    }

    [Fact]
    public async Task Reveal_ForAnUnknownDisk_LeavesTheSelectionAlone() {
        var viewModel = await LoadedAsync();
        var before = viewModel.SelectedDrive;

        viewModel.Reveal(diskNumber: 99);

        Assert.Same(before, viewModel.SelectedDrive);
    }

    // Held rather than dropped, but a disk that never appears must not strand the load either.
    [Fact]
    public async Task APendingRevealForAnUnknownDisk_FallsBackToTheNormalDefault() {
        var (page, release) = Gated();

        page.Reveal(diskNumber: 99);
        release();
        await page.LoadStorageAsync();

        Assert.Equal(SystemDiskNumber, page.SelectedDrive?.DiskNumber);
    }
}
