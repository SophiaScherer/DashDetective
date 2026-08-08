using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Reads the machine's static hardware facts for the Hardware tab's six spec cards.
///
/// The whole-surface façade the page consumes: one call, one aggregate, one refresh. Beneath it sits a
/// reader per card (<see cref="IProcessorInfoProvider"/> and friends) — the cards share no query, so
/// each owns its own reads and can be faked on its own. Implementations must never throw: each section
/// falls back to its own <c>.Unknown</c> record, so one dead source can't blank the others.
/// </summary>
internal interface IHardwareInfoProvider {
    Task<HardwareInfo> GetAsync();

    /// <summary>The reader set for this machine, or one that reports <c>.Unknown</c> for every card on a
    /// platform with no readers at all (what the old inline platform guard returned). This is the single
    /// place the platform is decided for the tab.</summary>
    static IHardwareInfoProvider ForCurrentPlatform() {
        if (OperatingSystem.IsWindows())
            return Windows();

        if (OperatingSystem.IsLinux())
            return Linux();

        return new UnsupportedHardwareInfoProvider();
    }

    /// <summary>The WMI readers. Carries the attribute because resolving them is the only Windows-specific
    /// part — <see cref="HardwareInfoProvider"/>'s composition and per-card guard are portable and stay
    /// callable from tests on every platform.</summary>
    [SupportedOSPlatform("windows")]
    private static IHardwareInfoProvider Windows() =>
        new HardwareInfoProvider(
            new WindowsProcessorInfoProvider(), new WindowsMemoryModulesProvider(),
            new WindowsStorageInfoProvider(), new WindowsMotherboardInfoProvider(),
            new WindowsGraphicsInfoProvider());

    /// <summary>
    /// The <c>/proc</c> and sysfs readers, filled in one milestone at a time — a card with no Linux reader
    /// yet keeps its <c>Unsupported*</c> one and renders "—". Storage arrives with the Storage milestone
    /// and graphics with the GPU one; per-DIMM memory modules need <c>dmidecode</c> with root and stay
    /// unsupported for good.
    ///
    /// Unlike <see cref="Windows"/> this carries no <see cref="SupportedOSPlatformAttribute"/>: the Linux
    /// readers are portable managed code over <c>IProcFileSystem</c>, so there is no annotated API for
    /// CA1416 to see and the attribute would be decoration rather than enforcement.
    /// </summary>
    private static IHardwareInfoProvider Linux() =>
        new HardwareInfoProvider(
            new LinuxProcessorInfoProvider(), new UnsupportedMemoryModulesProvider(),
            new UnsupportedStorageInfoProvider(), new LinuxMotherboardInfoProvider(),
            new UnsupportedGraphicsInfoProvider());
}
