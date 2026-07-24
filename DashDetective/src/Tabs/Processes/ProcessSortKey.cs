namespace DashDetective.Tabs.Processes;

/// <summary>
/// The column the process list is sorted by. Every key carries real data; ties break by name then PID.
/// </summary>
public enum ProcessSortKey {
    Name,
    Pid,
    Status,
    Cpu,
    Memory,
    Disk,
    Gpu,
}
