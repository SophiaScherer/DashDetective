using DashDetective.Services.Diagnostics;
using DashDetective.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>
/// Builds the Active Connections snapshot from <see cref="IConnectionsInterop"/> (TCP + UDP), resolves
/// each owning PID to a process name through <see cref="IProcessNameResolver"/>, and returns a sorted,
/// capped list. Runs off the UI thread via <see cref="GetAsync"/> and never throws (soft-fails to an empty
/// list), matching the app's provider convention. PID→name results are cached because the lookup costs a
/// process handle or a file read and most PIDs recur across polls; the cache evicts PIDs no longer present
/// (PIDs get reused).
///
/// Portable managed code — no platform prefix, because everything platform-specific about this tab is in
/// the two seams it is handed. Not thread-safe by design (the cache is per-instance mutable state): the
/// Network VM polls it from a single timer with an in-flight guard, so calls never overlap.
/// </summary>
internal sealed class ConnectionsProvider(
    IConnectionsInterop interop, IProcessNameResolver names) : IConnectionsProvider {
    /// <summary>Safety ceiling on rows returned, so a machine with a pathological number of sockets
    /// can't bloat memory. Ten times the UI's page size of 100 — the VM pages the full set client-side,
    /// only ever binding one page at a time — so this is a backstop, not the display cap, and it is what
    /// caps the pager at ten numbered pages.</summary>
    public const int MaxRows = 1000;

    private readonly Dictionary<int, string> _nameCache = new();

    public Task<ConnectionsSnapshot> GetAsync(CancellationToken token = default) => Task.Run(Snapshot, token);

    private ConnectionsSnapshot Snapshot() {
        try {
            var raw = new List<RawConnection>();
            raw.AddRange(interop.GetTcp());
            raw.AddRange(interop.GetUdp());

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
        } catch (Exception e) {
            Log.Warn("ConnectionsProvider read failed", e);
            return new ConnectionsSnapshot(Array.Empty<ConnectionInfo>(), 0);
        }
    }

    /// <summary>Formats an endpoint. IPv6 addresses are bracketed, because they contain colons themselves —
    /// unbracketed, <c>::1</c> port 631 reads as "::1:631", where the port is indistinguishable from another
    /// hextet. Also keeps <see cref="ConnectionInfo.Key"/> unambiguous, which the UI's keyed diff relies on.</summary>
    private static string Endpoint(IPAddress address, int port) {
        var host = address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();
        return $"{host}:{port.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>Resolves a PID to a display name through the platform's resolver, caching the result. The
    /// cache lives here rather than in the resolver because this class owns the snapshot, so it is the only
    /// one that knows when a PID has left.</summary>
    private string ResolveName(int pid) {
        if (_nameCache.TryGetValue(pid, out var cached))
            return cached;

        var name = names.Resolve(pid);
        _nameCache[pid] = name;
        return name;
    }

    private void EvictStalePids(HashSet<int> seenPids) {
        if (_nameCache.Count == 0)
            return;
        var stale = _nameCache.Keys.Where(pid => !seenPids.Contains(pid)).ToList();
        foreach (var pid in stale)
            _nameCache.Remove(pid);
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
        _ => Placeholders.Unknown,
    };
}
