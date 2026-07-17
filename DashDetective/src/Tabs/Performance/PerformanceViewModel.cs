using DashDetective.Shared;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// The Performance tab: a Task-Manager-style live resource drill-down. A left resource-selector rail
/// swaps a right detail pane (one large utilization chart + a stat-tile strip).
///
/// This is the initial UI implementation, driven by static mock data — the resource list, chart, and
/// stat tiles land across UI phases. Live sampling/providers (and the
/// <see cref="IRefreshablePage"/> / <see cref="ILiveSamplingPage"/> wiring) are a later technical pass.
///
/// Implements <see cref="ISelfScrollingPage"/> so the shell hosts it in the bounded, non-scrolling
/// container: the tab fills the viewport and manages its own panes (like File Explorer).
/// </summary>
public partial class PerformanceViewModel : ViewModelBase, ISelfScrollingPage {
}
