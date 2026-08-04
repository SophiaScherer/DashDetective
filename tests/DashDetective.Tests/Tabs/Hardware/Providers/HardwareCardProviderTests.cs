using DashDetective.Tabs.Hardware;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware.Providers;

/// <summary>Covers the five per-card readers behind <see cref="IHardwareInfoProvider"/>. They share one
/// contract — never throw, and hand back a fully populated record whose unreadable fields are "—" rather
/// than null — so they share one test class rather than five near-identical ones.
///
/// These run against real WMI, so they assert the contract rather than this machine's specs: a CI agent
/// is a VM with no DIMM part numbers and no PCIe slots, and must still produce a renderable record.</summary>
public class HardwareCardProviderTests {
    [Fact]
    public async Task Processor_GetAsync_ReturnsARenderableRecord() {
        if (!OperatingSystem.IsWindows())
            return;

        var info = await new WindowsProcessorInfoProvider().GetAsync();

        Assert.NotNull(info);
        Assert.NotEmpty(info.Name);
        Assert.NotEmpty(info.BaseBoost);
        Assert.NotEmpty(info.CacheL3);
    }

    [Fact]
    public async Task MemoryModules_GetAsync_ReturnsARenderableRecord() {
        if (!OperatingSystem.IsWindows())
            return;

        var info = await new WindowsMemoryModulesProvider().GetAsync();

        Assert.NotNull(info);
        Assert.NotEmpty(info.Summary);
        Assert.NotEmpty(info.Installed);
        Assert.NotEmpty(info.SlotsUsed);
    }

    /// <summary>The drive list is never null — the card iterates it to build its rows.</summary>
    [Fact]
    public async Task Storage_GetAsync_ReturnsARenderableRecord() {
        if (!OperatingSystem.IsWindows())
            return;

        var info = await new WindowsStorageInfoProvider().GetAsync();

        Assert.NotNull(info);
        Assert.NotNull(info.Drives);
        Assert.NotEmpty(info.Summary);
        Assert.All(info.Drives, drive => {
            Assert.NotEmpty(drive.Model);
            Assert.NotEmpty(drive.Detail);
        });
    }

    [Fact]
    public async Task Motherboard_GetAsync_ReturnsARenderableRecord() {
        if (!OperatingSystem.IsWindows())
            return;

        var info = await new WindowsMotherboardInfoProvider().GetAsync();

        Assert.NotNull(info);
        Assert.NotEmpty(info.Board);
        Assert.NotEmpty(info.Bios);
        Assert.NotEmpty(info.PcieSlots);
    }

    /// <summary>The adapter list is never null, and every adapter that survives the PCI-bus filter is
    /// named — an unnamed card would render a blank heading.</summary>
    [Fact]
    public async Task Graphics_GetAsync_ReturnsARenderableRecord() {
        if (!OperatingSystem.IsWindows())
            return;

        var info = await new WindowsGraphicsInfoProvider().GetAsync();

        Assert.NotNull(info);
        Assert.NotNull(info.Adapters);
        Assert.All(info.Adapters, adapter => {
            Assert.NotEmpty(adapter.Name);
            Assert.NotEmpty(adapter.Driver);
        });
    }

    /// <summary>Reading twice is stable — the readers hold no state, which is what lets the toolbar's
    /// Refresh re-run them and what lets one instance be shared.</summary>
    [Fact]
    public async Task Processor_ReadTwice_ReportsTheSameFacts() {
        if (!OperatingSystem.IsWindows())
            return;

        var provider = new WindowsProcessorInfoProvider();

        Assert.Equal(await provider.GetAsync(), await provider.GetAsync());
    }
}
