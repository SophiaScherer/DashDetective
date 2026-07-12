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

        var processes = Process.GetProcesses();
        var result = new List<ProcessInfo>(processes.Length);
        var nextCpuTime = new Dictionary<int, TimeSpan>(processes.Length);

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

                    result.Add(new ProcessInfo(pid, name, status, cpuPercent, memory, threads, category));
                } catch {
                    // A whole-process failure is rare (handle races) — skip that entry.
                }
            }
        }

        // Swap in the fresh CPU-time table so PIDs that have exited don't linger.
        PrevCpuTime.Clear();
        foreach (var pair in nextCpuTime)
            PrevCpuTime[pair.Key] = pair.Value;
        _prevSampledAt = now;

        return result;
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
