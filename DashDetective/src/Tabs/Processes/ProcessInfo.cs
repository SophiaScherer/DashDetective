namespace DashDetective.Tabs.Processes;

/// <summary>
/// An immutable snapshot of one process at a sampling instant. <see cref="MemoryBytes"/> is the process's
/// <b>private</b> working set — resident pages it doesn't share — which is what Task Manager's Memory column
/// reports; the total working set would count shared images against every process mapping them.
///
/// Carries the raw numeric keys
/// (<see cref="CpuPercent"/>, <see cref="MemoryBytes"/>, <see cref="DiskBytesPerSec"/>,
/// <see cref="GpuPercent"/>) the view model sorts on — the pre-formatted display strings live on
/// <see cref="ProcessRow"/> and can't be ordered. <see cref="Pid"/> is the identity used by the keyed
/// diff (unique among live processes). <see cref="ParentPid"/> ties each process to its creator so
/// <see cref="ProcessTreeBuilder"/> can collapse a multi-process app (e.g. all of Edge's helpers)
/// under one entry, the way Task Manager does.
/// </summary>
public sealed record ProcessInfo(
    int Pid,
    int ParentPid,
    string Name,
    string Status,
    double CpuPercent,
    long MemoryBytes,
    int ThreadCount,
    ProcessCategory Category,
    double DiskBytesPerSec,
    double GpuPercent);
