using DashDetective.Tabs.Hardware;
using DashDetective.Tests.Fakes;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware.Providers;

/// <summary>Covers <see cref="LinuxMotherboardInfoProvider"/>: the DMI fields it composes, the BIOS string
/// that has to match the shape the WMI arm produces, and the row that is permanently blank because Linux
/// has no rootless source for it.</summary>
public class LinuxMotherboardInfoProviderTests {
    private static Task<MotherboardInfo> Read(FakeProcFileSystem proc) =>
        new LinuxMotherboardInfoProvider(proc).GetAsync();

    private static FakeProcFileSystem WithBoard(string vendor, string name) =>
        new FakeProcFileSystem()
            .WithFile("/sys/class/dmi/id/board_vendor", vendor + "\n")
            .WithFile("/sys/class/dmi/id/board_name", name + "\n");

    /// <summary>What the VM acceptance check expects on screen.</summary>
    [Fact]
    public async Task GetAsync_ComposesTheBoardFromTheDmiVendorAndName() =>
        Assert.Equal("Oracle Corporation VirtualBox",
            (await Read(new FakeProcFileSystem().WithVirtualBoxDmi())).Board);

    /// <summary>The same "version (year)" shape the WMI arm composes, so the row reads identically on both
    /// platforms — even though the two read the date out of completely different encodings.</summary>
    [Fact]
    public async Task GetAsync_ComposesBiosAsVersionAndYear() =>
        Assert.Equal("VirtualBox (2006)",
            (await Read(new FakeProcFileSystem().WithVirtualBoxDmi())).Bios);

    /// <summary>A firmware with no date still reports its version rather than dropping the row.</summary>
    [Fact]
    public async Task GetAsync_WithNoBiosDate_ReportsTheVersionAlone() {
        var proc = new FakeProcFileSystem().WithFile("/sys/class/dmi/id/bios_version", "1203\n");

        Assert.Equal("1203", (await Read(proc)).Bios);
    }

    /// <summary>Falls back to the shared token scan for a board the catalog does not carry.</summary>
    [Fact]
    public async Task GetAsync_DerivesTheChipsetFromTheBoardName() =>
        Assert.Equal("AMD B650", (await Read(WithBoard("Micro-Star International Co., Ltd.", "MPG B650I EDGE"))).Chipset);

    /// <summary>
    /// Permanently "—", not a milestone yet to land. The WMI arm counts <c>Win32_SystemSlot</c> rows; the
    /// DMI equivalent is SMBIOS type 9, which the kernel does not surface under <c>/sys/class/dmi/id</c>.
    /// Counting <c>/sys/bus/pci</c> instead would report occupied devices — a different number under the
    /// same label.
    /// </summary>
    [Fact]
    public async Task GetAsync_NeverReportsAPcieSlotCount() =>
        Assert.Equal("—", (await Read(new FakeProcFileSystem().WithVirtualBoxDmi())).PcieSlots);

    /// <summary>An unpopulated DMI table is a snapshot of "—" rather than an exception.</summary>
    [Fact]
    public async Task GetAsync_WithNoDmiTable_ReportsDashesForEveryRow() {
        var info = await Read(new FakeProcFileSystem());

        Assert.Equal("—", info.Board);
        Assert.Equal("—", info.Chipset);
        Assert.Equal("—", info.Bios);
        Assert.Equal("—", info.FormFactor);
        Assert.Equal("—", info.M2Slots);
    }

    /// <summary>The root-only DMI files are never opened, so nothing logs a denial per read and no row
    /// depends on a value a normal user cannot get.</summary>
    [Fact]
    public async Task GetAsync_NeverReadsTheRootOnlyDmiFiles() {
        var proc = new FakeProcFileSystem().WithVirtualBoxDmi();

        _ = await Read(proc);

        Assert.DoesNotContain(proc.Reads, path =>
            path.EndsWith("serial", StringComparison.Ordinal)
            || path.EndsWith("uuid", StringComparison.Ordinal));
    }
}
