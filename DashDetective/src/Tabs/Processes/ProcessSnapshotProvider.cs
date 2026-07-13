using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Builds the process snapshot from managed <see cref="Process"/> enumeration. Runs off the UI thread
/// via <see cref="GetAsync"/> and never throws (soft-fails to an empty list), matching the app's
/// provider convention (<c>ConnectionsProvider</c> / <c>AdapterInfoProvider</c>).
///
/// CPU% has no direct API, so it's derived the way Task Manager does: the change in a process's
/// <see cref="Process.TotalProcessorTime"/> across the sampling interval, divided by (wall-clock ×
/// logical-processor count). The previous per-PID CPU times and the last sample time are held in
/// static state between calls; the table is swapped each snapshot so exited PIDs are evicted (PIDs get
/// reused). Not thread-safe by design — the VM polls from a single timer with an in-flight guard, so
/// calls never overlap.
/// </summary>
public static class ProcessSnapshotProvider {
    private static readonly Dictionary<int, TimeSpan> PrevCpuTime = new();
    private static readonly Dictionary<int, ulong> PrevIoBytes = new();
    private static DateTime _prevSampledAt;

    /// <summary>Logical processors — the divisor that normalises CPU% to Task Manager's 0–100 scale
    /// (a single fully-busy thread on a 12-thread box reads ~8%, not 100%).</summary>
    private static readonly int LogicalProcessors = Environment.ProcessorCount > 0 ? Environment.ProcessorCount : 1;

    public static Task<IReadOnlyList<ProcessInfo>> GetAsync() => Task.Run(Snapshot);

    private static IReadOnlyList<ProcessInfo> Snapshot() {
        if (!OperatingSystem.IsWindows())
            return Array.Empty<ProcessInfo>();

        var now = DateTime.UtcNow;
        // First snapshot has no prior point, so every CPU% reads 0 this pass and real next pass.
        var wallSeconds = _prevSampledAt == default ? 0 : (now - _prevSampledAt).TotalSeconds;

        // Per-process GPU% for this interval (PID → %), read once off the same PDH query.
        var gpuByPid = ProcessGpuSampler.Sample();

        var processes = Process.GetProcesses();
        var result = new List<ProcessInfo>(processes.Length);
        var nextCpuTime = new Dictionary<int, TimeSpan>(processes.Length);
        var nextIoBytes = new Dictionary<int, ulong>(processes.Length);

        foreach (var process in processes) {
            using (process) {
                try {
                    var pid = process.Id;

                    // Protected/exited processes deny TotalProcessorTime — treat as unavailable (0).
                    TimeSpan cpuTime;
                    try { cpuTime = process.TotalProcessorTime; } catch { cpuTime = TimeSpan.Zero; }
                    nextCpuTime[pid] = cpuTime;

                    var cpuPercent = ComputeCpuPercent(pid, cpuTime, wallSeconds);
                    var memory = SafeWorkingSet(process);
                    var threads = SafeThreadCount(process);
                    var status = SafeResponding(process) ? "Running" : "Not responding";
                    var category = HasMainWindow(process) ? ProcessCategory.App : ProcessCategory.Background;
                    var name = SafeName(process);
                    var disk = ComputeDiskRate(process, pid, nextIoBytes, wallSeconds);
                    var gpu = gpuByPid.TryGetValue(pid, out var g) ? g : 0;

                    result.Add(new ProcessInfo(pid, name, status, cpuPercent, memory, threads, category, disk, gpu));
                } catch {
                    // A whole-process failure is rare (handle races) — skip that entry.
                }
            }
        }

        // Swap in the fresh CPU-time / IO-byte tables so PIDs that have exited don't linger.
        Swap(PrevCpuTime, nextCpuTime);
        Swap(PrevIoBytes, nextIoBytes);
        _prevSampledAt = now;

        return result;
    }

    private static void Swap<T>(Dictionary<int, T> target, Dictionary<int, T> source) {
        target.Clear();
        foreach (var pair in source)
            target[pair.Key] = pair.Value;
    }

    /// <summary>Disk rate in bytes/sec: the change in cumulative read+write transfer bytes over the
    /// interval. Records the current total in <paramref name="nextIoBytes"/> for the next diff.</summary>
    private static double ComputeDiskRate(Process process, int pid, Dictionary<int, ulong> nextIoBytes, double wallSeconds) {
        if (!ProcessInterop.TryGetIoBytes(process, out var bytes))
            return 0;
        nextIoBytes[pid] = bytes;

        if (wallSeconds <= 0 || !PrevIoBytes.TryGetValue(pid, out var prev) || bytes < prev)
            return 0;
        return (bytes - prev) / wallSeconds;
    }

    private static double ComputeCpuPercent(int pid, TimeSpan cpuTime, double wallSeconds) {
        if (wallSeconds <= 0 || !PrevCpuTime.TryGetValue(pid, out var prev))
            return 0;

        var delta = (cpuTime - prev).TotalSeconds;
        if (delta <= 0)
            return 0;

        var percent = delta / (wallSeconds * LogicalProcessors) * 100;
        return percent < 0 ? 0 : percent > 100 ? 100 : percent;
    }

    private static bool HasMainWindow(Process process) {
        try { return process.MainWindowHandle != IntPtr.Zero; }
        catch { return false; }
    }

    private static long SafeWorkingSet(Process process) {
        try { return process.WorkingSet64; } catch { return 0; }
    }

    private static int SafeThreadCount(Process process) {
        try { return process.Threads.Count; } catch { return 0; }
    }

    // Processes without a message pump report Responding == true, so background processes read
    // "Running" and only a hung windowed app reads "Not responding" — matching Task Manager.
    private static bool SafeResponding(Process process) {
        try { return process.Responding; } catch { return true; }
    }

    private static string SafeName(Process process) {
        try { return process.ProcessName + ".exe"; } catch { return "Unknown"; }
    }
}
