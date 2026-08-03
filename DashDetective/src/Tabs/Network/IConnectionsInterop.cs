using System.Collections.Generic;
using System.Net;

namespace DashDetective.Tabs.Network;

/// <summary>One raw connection row from the OS tables: endpoints, TCP state (0 for UDP) and the
/// owning process id. Addresses/ports are already host-usable (port byte order swapped).</summary>
internal readonly record struct RawConnection(
    string Protocol, IPAddress LocalAddress, int LocalPort,
    IPAddress RemoteAddress, int RemotePort, uint State, int Pid);

/// <summary>
/// The OS connection tables — the netstat data the managed <c>IPGlobalProperties</c> API can't provide
/// because it omits the owning PID. This is the only Windows-specific piece of the Network tab;
/// everything above it is portable managed code. Implementations must never throw: a native failure
/// yields an empty list.
/// </summary>
internal interface IConnectionsInterop {
    /// <summary>All IPv4 TCP connections with owning PID and state. Empty on any failure.</summary>
    IReadOnlyList<RawConnection> GetTcp();

    /// <summary>All IPv4 UDP listeners with owning PID. Empty on any failure.</summary>
    IReadOnlyList<RawConnection> GetUdp();
}
