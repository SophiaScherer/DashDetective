using System;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Reads the machine's static hardware facts for the Hardware tab's six spec cards.
///
/// One interface over the whole surface rather than one per card: the public shape is already a single
/// method returning one aggregate, and the sections always fire together in a single pass so they can
/// share a query. Implementations must never throw — each section falls back to its own
/// <c>.Unknown</c> record, so one dead source can't blank the others.
/// </summary>
internal interface IHardwareInfoProvider {
    Task<HardwareInfo> GetAsync();

    /// <summary>The reader for this machine — WMI on Windows, and one that reports <c>.Unknown</c> for
    /// every card anywhere else (what the old inline platform guard returned).</summary>
    static IHardwareInfoProvider ForCurrentPlatform() =>
        OperatingSystem.IsWindows()
            ? new WindowsHardwareInfoProvider()
            : new UnsupportedHardwareInfoProvider();
}
