using DashDetective.Services.Platform.Linux;

namespace DashDetective.Tabs.Network;

/// <summary>
/// Names a process from <c>/proc</c>, through the <see cref="ProcPidName"/> derivation the Processes tab
/// uses — so the same process reads the same on both tabs rather than being truncated on one of them.
///
/// <b>No <c>.exe</c>, and no well-known PIDs.</b> Linux has no equivalent of the System or System Idle
/// processes; PID 4 is an ordinary kernel thread. A socket nobody could be attributed to shows "—" rather
/// than being blamed on PID 0, which is not a process.
///
/// Portable managed code over <see cref="IProcFileSystem"/>, so no <c>[SupportedOSPlatform]</c>; the
/// platform check lives in <see cref="IProcessNameResolver.ForCurrentPlatform"/>.
/// </summary>
internal sealed class LinuxProcessNameResolver : IProcessNameResolver {
    private readonly IProcFileSystem _proc;

    public LinuxProcessNameResolver() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so naming runs against canned fixtures from any dev
    /// machine.</summary>
    internal LinuxProcessNameResolver(IProcFileSystem proc) => _proc = proc;

    public string Resolve(int pid) {
        // Not a process: the owner of this socket could not be determined, which is what another user's
        // connection looks like from an unprivileged reader.
        if (pid == SocketInodeMap.NoPid)
            return "—";

        var name = ProcPidName.Read(_proc, pid);
        return name.Length > 0 ? name : IProcessNameResolver.Unnamed(pid);
    }
}
