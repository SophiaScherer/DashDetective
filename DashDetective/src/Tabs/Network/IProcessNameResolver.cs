using System;
using System.Globalization;
using System.Runtime.Versioning;

namespace DashDetective.Tabs.Network;

/// <summary>
/// Names the process owning a connection. The second platform-specific seam in this tab, because naming a
/// process is not portable even though looking one up is: the <c>.exe</c> suffix is a Windows convention,
/// and PIDs 0 and 4 mean specific things on Windows and ordinary things elsewhere.
///
/// Caching stays with <see cref="ConnectionsProvider"/>, which owns the snapshot and therefore knows when
/// a PID has gone. An implementation is a pure lookup and must never throw — an inaccessible or exited
/// process yields a placeholder.
/// </summary>
internal interface IProcessNameResolver {
    /// <summary>The display name for an owning PID.</summary>
    string Resolve(int pid);

    /// <summary>The resolver for this machine.</summary>
    static IProcessNameResolver ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? Windows()
        : OperatingSystem.IsLinux() ? new LinuxProcessNameResolver()
        : new UnsupportedProcessNameResolver();

    [SupportedOSPlatform("windows")]
    private static IProcessNameResolver Windows() => new WindowsProcessNameResolver();

    /// <summary>What every arm shows for a process it cannot name — the PID itself, which is still enough
    /// to find it with another tool. Shared so the platforms cannot drift into different wordings.</summary>
    static string Unnamed(int pid) => $"PID {pid.ToString(CultureInfo.InvariantCulture)}";
}
