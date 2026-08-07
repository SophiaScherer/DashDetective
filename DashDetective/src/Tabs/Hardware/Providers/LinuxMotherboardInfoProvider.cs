using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using DashDetective.Tabs.Hardware.Catalog;
using System;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Board facts from the rootless DMI files under <c>/sys/class/dmi/id</c> — vendor and product from the
/// board fields, BIOS version and release year from the firmware fields. Chipset, form factor and M.2
/// count have no source here any more than they do in WMI: form factor and M.2 come from
/// <see cref="HardwareCatalog"/>, and chipset falls back to the same
/// <see cref="ChipsetNames"/> token scan the Windows arm uses.
///
/// <b>PCIe slot count is permanently "—" on Linux.</b> The Windows arm counts <c>Win32_SystemSlot</c> rows;
/// its DMI equivalent is SMBIOS type 9, which the kernel does not surface under <c>/sys/class/dmi/id</c> —
/// only <c>dmidecode</c> reading <c>/dev/mem</c> as root can see it. <c>/sys/bus/pci</c> enumerates
/// occupied devices rather than physical slots, so counting it would report a different thing under the
/// same label.
/// </summary>
internal sealed class LinuxMotherboardInfoProvider : IMotherboardInfoProvider {
    private readonly IProcFileSystem _proc;

    public LinuxMotherboardInfoProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so a DMI tree — and its absence — can be exercised
    /// against canned fixtures from any dev machine.</summary>
    internal LinuxMotherboardInfoProvider(IProcFileSystem proc) => _proc = proc;

    public Task<MotherboardInfo> GetAsync() => Task.Run(Read);

    private MotherboardInfo Read() {
        try {
            var dmi = new DmiIdReader(_proc);
            var product = dmi.BoardName;
            var spec = HardwareCatalog.LookupBoard(product);
            var board = DmiIdReader.Join(dmi.BoardVendor, product);
            var chipset = spec?.Chipset ?? ChipsetNames.Derive(product);
            var bios = ReadBios(dmi);

            return new MotherboardInfo(
                Board: string.IsNullOrEmpty(board) ? "—" : board,
                Chipset: string.IsNullOrEmpty(chipset) ? "—" : chipset,
                Bios: string.IsNullOrEmpty(bios) ? "—" : bios,
                FormFactor: spec?.FormFactor ?? "—",
                PcieSlots: "—",
                M2Slots: spec?.M2Slots ?? "—");
        } catch (Exception e) {
            Log.Warn("MotherboardInfoProvider read failed", e);
            return MotherboardInfo.Unknown;
        }
    }

    /// <summary>BIOS version plus release year, e.g. "1203 (2024)" — the same shape the Windows arm
    /// composes, so the card reads identically on both platforms.</summary>
    private static string ReadBios(DmiIdReader dmi) {
        var version = dmi.BiosVersion;
        if (string.IsNullOrEmpty(version))
            return "";

        var year = DmiIdReader.Year(dmi.BiosDate);
        return year > 0 ? $"{version} ({year})" : version;
    }
}
