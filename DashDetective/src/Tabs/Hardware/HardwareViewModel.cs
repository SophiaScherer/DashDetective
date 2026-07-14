using DashDetective.Shared;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// The Hardware tab: a static, spec-sheet view of the machine's components — the design comp's
/// 2-column grid of cards (Processor, Graphics, Motherboard, Memory, Storage Devices, Sensors),
/// each an icon-tile header over a key/value spec list. The page scrolls as a whole like the
/// Dashboard, so it is deliberately NOT an <see cref="ISelfScrollingPage"/>.
///
/// This is the UI-only scaffold: the tab is reachable but shows only a placeholder until the card
/// grid lands in the next phase. Live/real data (WMI/PDH/sensors) is deferred to a later technical
/// phase, so there is no live-sampling (<see cref="ILiveSamplingPage"/>) or refresh
/// (<see cref="IRefreshablePage"/>) wiring here.
/// </summary>
public partial class HardwareViewModel : ViewModelBase {
}
