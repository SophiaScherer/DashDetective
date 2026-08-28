namespace DashDetective.Tabs.Processes;

/// <summary>A column of the Processes table. Declared in the order the table ships in; the user's own
/// order is resolved against that by <see cref="ProcessColumnOrder"/>.</summary>
public enum ProcessColumnId {
    Name,
    Pid,
    Status,
    Cpu,
    Memory,
    Disk,
    Gpu,
}
