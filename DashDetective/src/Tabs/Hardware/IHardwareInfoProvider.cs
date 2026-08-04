using System;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Reads the machine's static hardware facts for the Hardware tab's six spec cards.
///
/// The whole-surface façade the page consumes: one call, one aggregate, one refresh. Beneath it sits a
/// reader per card (<see cref="IProcessorInfoProvider"/> and friends) — the cards share no query, so
/// each owns its own WMI reads and can be faked on its own. Implementations must never throw: each
/// section falls back to its own <c>.Unknown</c> record, so one dead source can't blank the others.
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
