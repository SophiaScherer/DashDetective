using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Windows;
using DashDetective.Tabs.Hardware.Catalog;
using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Board facts: vendor + product from <c>Win32_BaseBoard</c>, BIOS version + release year from
/// <c>Win32_BIOS</c>, and a best-effort PCIe slot count from <c>Win32_SystemSlot</c> (slots whose
/// designation names PCI/PCIe). Chipset, form factor and M.2 count have no WMI source: form factor and
/// M.2 come from <see cref="HardwareCatalog"/>, and chipset falls back to a name-token derivation so most
/// boards resolve it without per-board data. The platform check lives in
/// <see cref="IHardwareInfoProvider.ForCurrentPlatform"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsMotherboardInfoProvider : IMotherboardInfoProvider {
    public Task<MotherboardInfo> GetAsync() => Task.Run(Read);

    private static MotherboardInfo Read() {
        try {
            var (manufacturer, product) = ReadBoardNames();
            var spec = HardwareCatalog.LookupBoard(product);
            var chipset = spec?.Chipset ?? ChipsetNames.Derive(product);
            var board = WmiRead.Join(manufacturer, product);
            var bios = ReadBios();
            var pcie = ReadPcieSlotCount();

            return new MotherboardInfo(
                Board: string.IsNullOrEmpty(board) ? "—" : board,
                Chipset: string.IsNullOrEmpty(chipset) ? "—" : chipset,
                Bios: string.IsNullOrEmpty(bios) ? "—" : bios,
                FormFactor: spec?.FormFactor ?? "—",
                PcieSlots: pcie > 0 ? pcie.ToString() : "—",
                M2Slots: spec?.M2Slots ?? "—");
        } catch (Exception e) {
            Log.Warn("MotherboardInfoProvider read failed", e);
            return MotherboardInfo.Unknown;
        }
    }

    /// <summary>Vendor and product in one pass over <c>Win32_BaseBoard</c> — each is the first non-empty
    /// value across the rows.</summary>
    private static (string Manufacturer, string Product) ReadBoardNames() {
        string manufacturer = "", product = "";
        WmiRead.ForEach("SELECT Manufacturer, Product FROM Win32_BaseBoard", obj => {
            if (string.IsNullOrEmpty(manufacturer))
                manufacturer = WmiRead.Text(obj, "Manufacturer");
            if (string.IsNullOrEmpty(product))
                product = WmiRead.Text(obj, "Product");
        });

        return (manufacturer, product);
    }

    /// <summary>BIOS version plus release year, e.g. "1203 (2024)"; both read in one pass.</summary>
    private static string ReadBios() {
        string version = "", releaseDate = "";
        WmiRead.ForEach("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", obj => {
            if (string.IsNullOrEmpty(version))
                version = WmiRead.Text(obj, "SMBIOSBIOSVersion");
            if (string.IsNullOrEmpty(releaseDate))
                releaseDate = WmiRead.Text(obj, "ReleaseDate");
        });

        if (string.IsNullOrEmpty(version))
            return "";

        var year = WmiRead.DmtfYear(releaseDate);
        return year > 0 ? $"{version} ({year})" : version;
    }

    /// <summary>Best-effort count of PCIe slots — <c>Win32_SystemSlot</c> rows whose designation names
    /// PCI/PCIe. Lane width isn't in WMI, so only the count is reported.</summary>
    private static int ReadPcieSlotCount() {
        try {
            var count = 0;
            WmiRead.ForEach("SELECT SlotDesignation FROM Win32_SystemSlot", obj => {
                var designation = obj["SlotDesignation"] as string ?? "";
                if (designation.Contains("PCI", StringComparison.OrdinalIgnoreCase))
                    count++;
            });

            return count;
        } catch (Exception e) {
            Log.Warn("MotherboardInfoProvider PCIe slot count read failed", e);
            return 0;
        }
    }
}
