using System.Diagnostics;
using System.Runtime.Versioning;

namespace DashDetective.Tabs.Network;

/// <summary>
/// Names a process the way the rest of the Windows UI does: the image name with <c>.exe</c>, plus the two
/// well-known PIDs that own sockets but are not ordinary processes.
///
/// The lookup itself is portable managed code — the constructor carries
/// <see cref="SupportedOSPlatformAttribute"/> because what the class *encodes* is Windows-only. PID 4 is
/// the System process here and an ordinary kernel thread on Linux, so running this off Windows would
/// mislabel real rows rather than fail. Annotating the constructor is what forces
/// <see cref="IProcessNameResolver.ForCurrentPlatform"/> to hold a guard.
/// </summary>
internal sealed class WindowsProcessNameResolver : IProcessNameResolver {
    [SupportedOSPlatform("windows")]
    public WindowsProcessNameResolver() { }

    public string Resolve(int pid) {
        if (pid == 0)
            return "System Idle";
        if (pid == 4)
            return "System";

        try {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName + ".exe";
        } catch {
            // ArgumentException (exited) or Win32Exception (access denied on a protected process).
            return IProcessNameResolver.Unnamed(pid);
        }
    }
}

/// <summary>Names nothing, for a platform whose connection tables are unread anyway — with no rows there
/// is no owner to name, so this exists to keep the seam total rather than to be used.</summary>
internal sealed class UnsupportedProcessNameResolver : IProcessNameResolver {
    public string Resolve(int pid) => IProcessNameResolver.Unnamed(pid);
}
