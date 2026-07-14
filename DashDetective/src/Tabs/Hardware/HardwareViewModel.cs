using System.Collections.Generic;
using DashDetective.Shared;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// The Hardware tab: a static, spec-sheet view of the machine's components — the design comp's
/// 2-column grid of cards (Processor, Graphics, Motherboard, Memory, Storage Devices, Sensors),
/// each an icon-tile header over a key/value spec list. The page scrolls as a whole like the
/// Dashboard, so it is deliberately NOT an <see cref="ISelfScrollingPage"/>.
///
/// This is the UI-only phase: the card structure and labels are in place but every value is the
/// neutral placeholder "—". Live/real data (WMI/PDH/sensors) is deferred to a later technical
/// phase, so there is no live-sampling (<see cref="ILiveSamplingPage"/>) or refresh
/// (<see cref="IRefreshablePage"/>) wiring here — a provider will just populate these models.
/// </summary>
public partial class HardwareViewModel : ViewModelBase {
    /// <summary>The six component cards, in comp order, bound by the view's 2-column grid. Fixed for
    /// now; a later phase swaps the "—" placeholders for real readings (and may make this
    /// observable).</summary>
    public IReadOnlyList<HardwareCard> Cards { get; } = new[] {
        new HardwareCard("Processor", "—", HardwareIcons.Chip,
            HardwareIcons.Blue, HardwareIcons.BlueBg, new[] {
                new HardwareSpec("Cores / Threads"),
                new HardwareSpec("Base / Boost"),
                new HardwareSpec("Cache (L3)"),
                new HardwareSpec("TDP"),
                new HardwareSpec("Socket"),
            }),
        new HardwareCard("Graphics", "—", HardwareIcons.Graph,
            HardwareIcons.Green, HardwareIcons.GreenBg, new[] {
                new HardwareSpec("Memory"),
                new HardwareSpec("CUDA Cores"),
                new HardwareSpec("Boost Clock"),
                new HardwareSpec("Driver"),
                new HardwareSpec("Bus"),
            }),
        new HardwareCard("Motherboard", "—", HardwareIcons.Grid,
            HardwareIcons.Purple, HardwareIcons.PurpleBg, new[] {
                new HardwareSpec("Chipset"),
                new HardwareSpec("BIOS"),
                new HardwareSpec("Form Factor"),
                new HardwareSpec("PCIe Slots"),
                new HardwareSpec("M.2 Slots"),
            }),
        new HardwareCard("Memory", "—", HardwareIcons.Bars,
            HardwareIcons.Yellow, HardwareIcons.YellowBg, new[] {
                new HardwareSpec("Installed"),
                new HardwareSpec("Speed"),
                new HardwareSpec("Timings"),
                new HardwareSpec("Slots used"),
                new HardwareSpec("Voltage"),
            }),
        new HardwareCard("Storage Devices", "—", HardwareIcons.Bars,
            HardwareIcons.Orange, HardwareIcons.OrangeBg, new[] {
                new HardwareSpec("Drive 1"),
                new HardwareSpec("Drive 2"),
                new HardwareSpec("Drive 3"),
                new HardwareSpec("Total Health"),
            }),
        new HardwareCard("Sensors", "—", HardwareIcons.Graph,
            HardwareIcons.Red, HardwareIcons.RedBg, new[] {
                new HardwareSpec("CPU Package"),
                new HardwareSpec("GPU Core"),
                new HardwareSpec("System"),
                new HardwareSpec("CPU Fan"),
                new HardwareSpec("VCore"),
            }),
    };
}
