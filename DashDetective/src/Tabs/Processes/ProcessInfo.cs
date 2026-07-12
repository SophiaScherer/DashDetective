namespace DashDetective.Tabs.Processes;

/// <summary>
/// An immutable snapshot of one process at a sampling instant. Carries the raw numeric keys
/// (<see cref="CpuPercent"/>, <see cref="MemoryBytes"/>) the view model sorts on — the pre-formatted
/// display strings live on <see cref="ProcessRow"/> and can't be ordered. <see cref="Pid"/> is the
/// identity used by the keyed diff (unique among live processes).
///
/// Disk and GPU usage are added in a later phase; per-process Network throughput is deferred (no
/// clean in-box API), so the Net column stays "—".
/// </summary>
public sealed record ProcessInfo(
    int Pid,
    string Name,
    string Status,
    double CpuPercent,
    long MemoryBytes,
    int ThreadCount,
    ProcessCategory Category);
