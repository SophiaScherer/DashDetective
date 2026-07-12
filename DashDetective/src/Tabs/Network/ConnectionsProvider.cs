using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>The connections snapshot: the (capped) rows to display plus the true total before
/// capping, so the panel can report an honest count even when the list is truncated.</summary>
public sealed record ConnectionsSnapshot(IReadOnlyList<ConnectionInfo> Rows, int Total);

/// <summary>
/// Builds the Active Connections snapshot from <see cref="ConnectionsInterop"/> (TCP + UDP), resolves
/// each owning PID to a process name, and returns a sorted, capped list. Runs off the UI thread via
/// <see cref="GetAsync"/> and never throws (soft-fails to an empty list), matching the app's provider
/// convention. PID→name results are cached because <see cref="Process.GetProcessById"/> is relatively
/// costly and most PIDs recur across polls; the cache evicts PIDs no longer present (PIDs get reused).
///
/// Not thread-safe by design: the Network VM polls it from a single timer with an in-flight guard, so
/// calls never overlap.
/// </summary>
public static class ConnectionsProvider {
    /// <summary>Upper bound on rows returned, so a machine with thousands of sockets can't bloat the UI.</summary>
    public const int MaxRows = 100;

    private static readonly Dictionary<int, string> NameCache = new();

    public static Task<ConnectionsSnapshot> GetAsync() => Task.Run(Snapshot);

    private static ConnectionsSnapshot Snapshot() {
        try {
            var raw = new List<RawConnection>();
            raw.AddRange(ConnectionsInterop.GetTcp());
            raw.AddRange(ConnectionsInterop.GetUdp());

            var seenPids = new HashSet<int>();
            // De-duplicate by identity key: two rows can share Protocol|Local|Remote|Pid (e.g. UDP
            // sockets with the same PID + local endpoint), and the UI keys rows by this — duplicates
            // would break the keyed diff (an out-of-range Move) and must not reach it.
            var seenKeys = new HashSet<string>();
            var list = new List<ConnectionInfo>(raw.Count);
            foreach (var c in raw) {
                seenPids.Add(c.Pid);
                var process = ResolveName(c.Pid);
                var local = Endpoint(c.LocalAddress, c.LocalPort);
                var remote = c.Protocol == "UDP" ? "—" : Endpoint(c.RemoteAddress, c.RemotePort);
                var state = c.Protocol == "UDP" ? "—" : TcpState(c.State);
                var info = new ConnectionInfo(process, local, remote, state, c.Protocol, c.Pid);
                if (seenKeys.Add(info.Key))
                    list.Add(info);
            }

            EvictStalePids(seenPids);

            list.Sort(static (a, b) => {
                var byProcess = string.Compare(a.Process, b.Process, StringComparison.OrdinalIgnoreCase);
                if (byProcess != 0) return byProcess;
                var byRemote = string.Compare(a.RemoteEndpoint, b.RemoteEndpoint, StringComparison.Ordinal);
                if (byRemote != 0) return byRemote;
                return string.Compare(a.LocalEndpoint, b.LocalEndpoint, StringComparison.Ordinal);
            });

            var total = list.Count;
            if (list.Count > MaxRows)
                list = list.GetRange(0, MaxRows);
            return new ConnectionsSnapshot(list, total);
        } catch {
            return new ConnectionsSnapshot(Array.Empty<ConnectionInfo>(), 0);
        }
    }

    private static string Endpoint(IPAddress address, int port) =>
        $"{address}:{port.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Resolves a PID to "name.exe", using well-known ids and a cache. Inaccessible (elevated/
    /// protected) or already-exited processes fall back to "PID n" rather than throwing.</summary>
    private static string ResolveName(int pid) {
        if (pid == 0)
            return "System Idle";
        if (pid == 4)
            return "System";
        if (NameCache.TryGetValue(pid, out var cached))
            return cached;

        string name;
        try {
            using var process = Process.GetProcessById(pid);
            name = process.ProcessName + ".exe";
        } catch {
            // ArgumentException (exited) or Win32Exception (access denied on a protected process).
            name = $"PID {pid.ToString(CultureInfo.InvariantCulture)}";
        }

        NameCache[pid] = name;
        return name;
    }

    private static void EvictStalePids(HashSet<int> seenPids) {
        if (NameCache.Count == 0)
            return;
        var stale = NameCache.Keys.Where(pid => !seenPids.Contains(pid)).ToList();
        foreach (var pid in stale)
            NameCache.Remove(pid);
    }

    /// <summary>Maps a MIB_TCP_STATE value to a display label (only a few are colour-coded specially;
    /// the rest render in the neutral "other" colour).</summary>
    private static string TcpState(uint state) => state switch {
        1 => "Closed",
        2 => "Listening",
        3 => "Syn-sent",
        4 => "Syn-received",
        5 => "Established",
        6 => "Fin-wait-1",
        7 => "Fin-wait-2",
        8 => "Close-wait",
        9 => "Closing",
        10 => "Last-ack",
        11 => "Time-wait",
        12 => "Delete-tcb",
        _ => "Unknown",
    };
}
