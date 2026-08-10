using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Maps a socket inode to the process holding it, by walking every process's open file descriptors —
/// the only rootless way to attribute a socket, since <c>/proc/net/*</c> names an inode and never a PID.
///
/// <b>Stateful and cached, deliberately.</b> The walk is a <c>readlink</c> per descriptor across every
/// process — thousands of calls — and the connections table polls every 2.5 seconds. So the map is kept
/// between calls and <see cref="Refresh"/> only walks when it is asked about an inode it has never seen;
/// a steady-state poll costs nothing. Held by the interop rather than by a bundle member, the
/// <c>IPhysicalDiskThroughputSampler</c> precedent for a reader that may hold state.
///
/// <b>Permission-limited by design.</b> Listing another user's <c>/proc/[pid]/fd</c> is denied, which the
/// filesystem seam reports as an empty listing — so other users' sockets simply go unattributed, at the
/// cost of one failed call per process rather than a uid read per process.
/// </summary>
internal sealed class SocketInodeMap(IProcFileSystem proc) {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string ProcRoot = "/proc/";

    /// <summary>What the kernel writes as a socket descriptor's link target, as <c>socket:[12345]</c>.</summary>
    private const string SocketPrefix = "socket:[";

    /// <summary>No owner. The kernel's own value for a socket with no inode, and what an unattributable
    /// socket resolves to.</summary>
    internal const int NoPid = 0;

    private readonly Dictionary<long, int> _pidByInode = [];

    /// <summary>Rebuilds the map if any of <paramref name="inodes"/> is unknown, and at most once per call.
    /// A rebuild is wholesale, so inodes of closed sockets fall out rather than accumulating.</summary>
    internal void Refresh(IReadOnlyCollection<long> inodes) {
        if (!NeedsWalk(inodes))
            return;

        _pidByInode.Clear();
        foreach (var pid in ProcPids.List(proc)) {
            var fdRoot = ProcRoot + pid.ToString(CultureInfo.InvariantCulture) + "/fd";

            foreach (var descriptor in proc.ListDirectory(fdRoot)) {
                if (proc.ResolveLink(fdRoot + "/" + descriptor) is not { } target
                    || InodeOf(target) is not { } inode)
                    continue;

                // Lowest PID wins. A socket can be held by several processes (a fork, or one passed over a
                // unix socket), and the row's identity key carries the PID — so an unstable choice would
                // make the same connection change key between polls and break the UI's keyed diff.
                // "Whichever the walk saw first" is NOT stable: /proc listing order is unspecified.
                if (!_pidByInode.TryGetValue(inode, out var holder) || pid < holder)
                    _pidByInode[inode] = pid;
            }
        }
    }

    /// <summary>The process holding a socket, or <see cref="NoPid"/> when it is unknown — another user's,
    /// or closed since the last walk.</summary>
    internal int PidFor(long inode) =>
        _pidByInode.TryGetValue(inode, out var pid) ? pid : NoPid;

    /// <summary>
    /// The inode in a descriptor's link target. The marker is searched for anywhere in the string rather
    /// than anchored, because <c>socket:[12345]</c> is not a real path: the production
    /// <c>IProcFileSystem</c> resolves link targets to full paths, so it hands back
    /// <c>/proc/1234/fd/socket:[12345]</c> rather than the bare target. Both forms read the same here.
    /// <c>null</c> for a descriptor that is not a socket — a file, a pipe, an eventfd.
    /// </summary>
    internal static long? InodeOf(string linkTarget) {
        var start = linkTarget.IndexOf(SocketPrefix, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += SocketPrefix.Length;
        var end = linkTarget.IndexOf(']', start);
        if (end < 0)
            return null;

        return long.TryParse(
            linkTarget.AsSpan(start, end - start),
            NumberStyles.None, CultureInfo.InvariantCulture, out var inode)
            ? inode
            : null;
    }

    /// <summary>Whether anything asked about is missing. Inode 0 is the kernel's "no inode" and is never
    /// worth a walk.</summary>
    private bool NeedsWalk(IReadOnlyCollection<long> inodes) {
        foreach (var inode in inodes) {
            if (inode != 0 && !_pidByInode.ContainsKey(inode))
                return true;
        }

        return false;
    }
}
