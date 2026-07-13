namespace DashDetective.Tabs.Processes;

/// <summary>
/// The column the process list is sorted by. Disk / Network / Gpu are wired now but have no data yet
/// (Disk/Gpu arrive in a later phase; Network is deferred), so sorting by them currently falls through
/// to the name tie-break.
/// </summary>
public enum ProcessSortKey {
    Name,
    Pid,
    Status,
    Cpu,
    Memory,
    Disk,
    Network,
    Gpu,
}
