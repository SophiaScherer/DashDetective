using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Versioning;

namespace DashDetective.Tabs.Network;

/// <summary>One raw connection row from the OS tables: endpoints, TCP state (0 for UDP) and the
/// owning process id. Addresses/ports are already host-usable (port byte order swapped).</summary>
internal readonly record struct RawConnection(
    string Protocol, IPAddress LocalAddress, int LocalPort,
    IPAddress RemoteAddress, int RemotePort, uint State, int Pid);

/// <summary>
/// The OS connection tables — the netstat data the managed <c>IPGlobalProperties</c> API can't provide
/// because it omits the owning PID. This is the only platform-specific piece of the Network tab;
/// everything above it is portable managed code. Implementations must never throw: a read failure
/// yields an empty list.
///
/// <b>Address families are whatever the platform can supply, not a fixed set.</b> The Linux reader
/// includes IPv6; the Windows one is IPv4-only, because the <c>OWNER_PID</c> tables use different
/// 16-byte-address structs for IPv6 that have not been written yet.
/// </summary>
internal interface IConnectionsInterop {
    /// <summary>All TCP connections with owning PID and state. State is a <c>MIB_TCP_STATE</c> value
    /// whatever the platform's own numbering is. Empty on any failure.</summary>
    IReadOnlyList<RawConnection> GetTcp();

    /// <summary>All UDP listeners with owning PID. UDP is reported as connectionless — no remote endpoint
    /// and no state — even where the platform tracks them. Empty on any failure.</summary>
    IReadOnlyList<RawConnection> GetUdp();

    /// <summary>The connection tables for this machine, or a reader that reports none. The platform is
    /// decided here and nowhere else; <see cref="NetworkProviders"/> takes whatever it is handed.</summary>
    static IConnectionsInterop ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? Windows()
        : OperatingSystem.IsLinux() ? new LinuxConnectionsInterop()
        : new UnsupportedConnectionsInterop();

    [SupportedOSPlatform("windows")]
    private static IConnectionsInterop Windows() => new WindowsConnectionsInterop();
}
