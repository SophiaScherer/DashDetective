using System;
using DashDetective.Shared;

namespace DashDetective.Tabs.Network;

/// <summary>
/// The Network tab: adapters, throughput, connections and diagnostics. Like the Dashboard it is
/// always-on — constructed once by the shell and left running for the app's lifetime — so it
/// implements <see cref="IRefreshablePage"/> (toolbar Refresh) and <see cref="IDisposable"/>.
///
/// Phase 0 scaffold: the page renders its six-panel layout with placeholder content; the live
/// samplers/providers are wired in later phases.
/// </summary>
public partial class NetworkViewModel : ViewModelBase, IRefreshablePage, IDisposable {
    public void Refresh() {
        // No live data yet (Phase 0 scaffold).
    }

    public void Dispose() {
        // No timers/handles yet (Phase 0 scaffold).
    }
}
