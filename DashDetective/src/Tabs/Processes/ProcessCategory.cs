namespace DashDetective.Tabs.Processes;

/// <summary>
/// Which group a process belongs to in the list, mirroring Task Manager's split: a foreground
/// <see cref="App"/> (it owns a visible top-level window) or a <see cref="Background"/> process
/// (services, helpers and Windows components with no window). Determined from
/// <c>Process.MainWindowHandle</c> in <see cref="ProcessSnapshotProvider"/>.
/// </summary>
public enum ProcessCategory {
    App,
    Background,
}
