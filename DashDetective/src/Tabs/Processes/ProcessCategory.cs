namespace DashDetective.Tabs.Processes;

/// <summary>
/// Which group a process belongs to in the list, mirroring Task Manager's three-way split:
/// a foreground <see cref="App"/> (it owns a visible top-level window on the interactive desktop),
/// a <see cref="Background"/> process (a user-session process with no window — helpers, trays,
/// updaters), or a <see cref="Windows"/> process (a system/service process outside the interactive
/// session — svchost, csrss, services.exe and the like). Determined by <see cref="ProcessClassifier"/>
/// and consumed by <see cref="ProcessSnapshotProvider"/>.
/// </summary>
public enum ProcessCategory {
    App,
    Background,
    Windows,
}
