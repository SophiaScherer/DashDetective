namespace DashDetective.Tabs.Processes;

/// <summary>
/// An immutable snapshot of one process at a sampling instant. Carries the raw numeric keys
/// (<see cref="CpuPercent"/>, <see cref="MemoryBytes"/>, <see cref="DiskBytesPerSec"/>,
/// <see cref="GpuPercent"/>) the view model sorts on — the pre-formatted display strings live on
/// <see cref="ProcessRow"/> and can't be ordered. <see cref="Pid"/> is the identity used by the keyed
/// diff (unique among live processes).
///
/// Per-process Network throughput is deferred (no clean in-box API), so the Net column stays "—".
/// </summary>
public sealed record ProcessInfo(
    int Pid,
    string Name,
    string Status,
    double CpuPercent,
    long MemoryBytes,
    int ThreadCount,
    ProcessCategory Category,
    double DiskBytesPerSec,
    double GpuPercent);
