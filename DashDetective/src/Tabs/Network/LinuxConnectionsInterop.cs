using DashDetective.Services.Platform.Linux;
using System.Collections.Generic;
using System.Net;

namespace DashDetective.Tabs.Network;

/// <summary>
/// The Linux connection tables, from <c>/proc/net/{tcp,tcp6,udp,udp6}</c>. Stands opposite
/// <see cref="WindowsConnectionsInterop"/>, and like it reports an empty list on any failure rather than
/// throwing. Portable managed code over <see cref="IProcFileSystem"/>, so it carries no
/// <c>[SupportedOSPlatform]</c>; the platform check lives in
/// <see cref="NetworkProviders.ForCurrentPlatform"/>.
///
/// <b>It translates the kernel's TCP state codes to the MIB numbering</b> that
/// <see cref="RawConnection.State"/> is defined in. The two tables are unrelated — Linux LISTEN is
/// <c>0x0A</c> where MIB 10 is Last-ack, Linux ESTABLISHED is <c>0x01</c> where MIB 1 is Closed — so
/// passing them through would label every row wrongly and plausibly.
///
/// <b>IPv6 is included, unlike the Windows side</b>, which reads only the IPv4 OWNER_PID tables. On Linux
/// most listeners bind <c>::</c>, so omitting them would leave the panel looking broken next to
/// <c>ss -tunap</c>.
/// </summary>
internal sealed class LinuxConnectionsInterop : IConnectionsInterop {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string TcpPath = "/proc/net/tcp";
    private const string Tcp6Path = "/proc/net/tcp6";
    private const string UdpPath = "/proc/net/udp";
    private const string Udp6Path = "/proc/net/udp6";

    private readonly IProcFileSystem _proc;
    private readonly SocketInodeMap _inodes;

    public LinuxConnectionsInterop() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the whole read runs against canned fixtures from any
    /// dev machine.</summary>
    internal LinuxConnectionsInterop(IProcFileSystem proc) {
        _proc = proc;
        _inodes = new SocketInodeMap(proc);
    }

    public IReadOnlyList<RawConnection> GetTcp() => Read("TCP", TcpPath, Tcp6Path);

    public IReadOnlyList<RawConnection> GetUdp() => Read("UDP", UdpPath, Udp6Path);

    /// <summary>Reads a protocol's two address-family tables and attributes each socket to its owner. The
    /// inode walk is wholesale, so the one this triggers also maps the other protocol's sockets and the
    /// second call finds them already known.</summary>
    private IReadOnlyList<RawConnection> Read(string protocol, string v4Path, string v6Path) {
        var sockets = new List<ProcNetSocket>();
        sockets.AddRange(ProcNetParser.Parse(_proc.ReadAllLines(v4Path)));
        sockets.AddRange(ProcNetParser.Parse(_proc.ReadAllLines(v6Path)));

        var inodes = new List<long>(sockets.Count);
        foreach (var socket in sockets)
            inodes.Add(socket.Inode);
        _inodes.Refresh(inodes);

        var isUdp = protocol == "UDP";
        var rows = new List<RawConnection>(sockets.Count);
        foreach (var socket in sockets) {
            rows.Add(new RawConnection(
                protocol,
                socket.LocalAddress, socket.LocalPort,
                // UDP is presented as connectionless on both platforms, so a connected UDP socket's peer
                // and state are dropped here rather than reaching a table whose UDP rows show "—".
                isUdp ? IPAddress.Any : socket.RemoteAddress,
                isUdp ? 0 : socket.RemotePort,
                isUdp ? 0 : MibState(socket.State),
                _inodes.PidFor(socket.Inode)));
        }

        return rows;
    }

    /// <summary>
    /// Maps a kernel TCP state to the <c>MIB_TCP_STATE</c> value the display table is keyed by. The 8 → 8
    /// row is a coincidence of two unrelated numberings, not a shared code. <c>NEW_SYN_RECV</c> is a
    /// half-open connection the kernel tracks separately and reports as Syn-received, which is what it is.
    /// Anything unrecognised maps to 0, which renders as "Unknown".
    /// </summary>
    internal static uint MibState(int kernelState) => kernelState switch {
        0x01 => 5,      // ESTABLISHED
        0x02 => 3,      // SYN_SENT
        0x03 => 4,      // SYN_RECV
        0x04 => 6,      // FIN_WAIT1
        0x05 => 7,      // FIN_WAIT2
        0x06 => 11,     // TIME_WAIT
        0x07 => 1,      // CLOSE
        0x08 => 8,      // CLOSE_WAIT
        0x09 => 10,     // LAST_ACK
        0x0A => 2,      // LISTEN
        0x0B => 9,      // CLOSING
        0x0C => 4,      // NEW_SYN_RECV
        _ => 0,
    };
}
