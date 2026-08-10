using DashDetective.Services.Platform.Linux;
using DashDetective.Tabs.Network;
using DashDetective.Tests.Fakes;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Network;

/// <summary>Covers <see cref="LinuxConnectionsInterop"/>: the state translation between two unrelated
/// numberings, the IPv4 + IPv6 union, the owner attribution, and the UDP row shape that has to match what
/// the Windows reader produces.</summary>
public class LinuxConnectionsInteropTests {
    /// <summary>The four socket tables plus the descriptors that attribute them. 21344 belongs to pid 812
    /// and 48219 to pid 1201; the rest are left unattributed, which is what another user's socket looks
    /// like.</summary>
    private static FakeProcFileSystem Proc() =>
        new FakeProcFileSystem()
            .WithFile("/proc/net/tcp", ProcFixtures.ProcNetTcp)
            .WithFile("/proc/net/tcp6", ProcFixtures.ProcNetTcp6)
            .WithFile("/proc/net/udp", ProcFixtures.ProcNetUdp)
            .WithFile("/proc/net/udp6", ProcFixtures.ProcNetUdp6)
            .WithLink("/proc/812/fd/3", "/proc/812/fd/socket:[21344]")
            .WithLink("/proc/1201/fd/7", "/proc/1201/fd/socket:[48219]");

    private static LinuxConnectionsInterop Interop() => new(Proc());

    /// <summary>
    /// The milestone's central correctness claim. Linux and Windows number TCP states differently and the
    /// display table is keyed by the Windows one, so every row would be labelled wrongly — and
    /// plausibly — without this. LISTEN is the clearest case: Linux 0x0A passed through unmapped is MIB 10,
    /// which renders as "Last-ack".
    /// </summary>
    [Theory]
    [InlineData(0x01, 5u)]      // ESTABLISHED
    [InlineData(0x02, 3u)]      // SYN_SENT
    [InlineData(0x03, 4u)]      // SYN_RECV
    [InlineData(0x04, 6u)]      // FIN_WAIT1
    [InlineData(0x05, 7u)]      // FIN_WAIT2
    [InlineData(0x06, 11u)]     // TIME_WAIT
    [InlineData(0x07, 1u)]      // CLOSE
    [InlineData(0x08, 8u)]      // CLOSE_WAIT — the one number the two tables share, by coincidence
    [InlineData(0x09, 10u)]     // LAST_ACK
    [InlineData(0x0A, 2u)]      // LISTEN
    [InlineData(0x0B, 9u)]      // CLOSING
    [InlineData(0x0C, 4u)]      // NEW_SYN_RECV
    [InlineData(0x00, 0u)]      // not a state the kernel writes
    [InlineData(0x7F, 0u)]      // a state a later kernel might add
    public void MibState_TranslatesTheKernelNumbering(int kernel, uint expected) =>
        Assert.Equal(expected, LinuxConnectionsInterop.MibState(kernel));

    /// <summary>Read end to end, the LISTEN and ESTABLISHED rows come back in the display numbering.</summary>
    [Fact]
    public void GetTcp_TranslatesStatesOnRealRows() {
        var rows = Interop().GetTcp();

        var listener = rows.Single(r => r.LocalPort == 22);
        Assert.Equal(2u, listener.State);

        var established = rows.Single(r => r.LocalPort == 52010);
        Assert.Equal(5u, established.State);
    }

    /// <summary>Both address families land in one list — the Windows reader's IPv4-only behaviour is a
    /// limitation of its tables, not a contract the Linux one has to match.</summary>
    [Fact]
    public void GetTcp_IncludesIpv4AndIpv6() {
        var rows = Interop().GetTcp();

        Assert.Equal(7, rows.Count);
        Assert.Contains(rows, r => r.LocalAddress.ToString() == "127.0.0.1");
        Assert.Contains(rows, r => r.LocalAddress.ToString() == "::");
    }

    /// <summary>A socket whose inode is held by a listed process is attributed to it.</summary>
    [Fact]
    public void GetTcp_AttributesSocketsToTheirOwningProcess() {
        var rows = Interop().GetTcp();

        Assert.Equal(812, rows.Single(r => r.LocalPort == 53).Pid);
        Assert.Equal(1201, rows.Single(r => r.LocalPort == 52010).Pid);
    }

    /// <summary>Another user's socket, or one closed since the walk, reports no PID rather than a wrong
    /// one — the caller turns that into a placeholder.</summary>
    [Fact]
    public void GetTcp_UnattributableSocket_ReportsNoPid() =>
        Assert.Equal(SocketInodeMap.NoPid, Interop().GetTcp().Single(r => r.LocalPort == 22).Pid);

    /// <summary>
    /// UDP keeps the Windows row shape: no remote endpoint and no state, which is what the table renders as
    /// "—". The fixture's third row is a CONNECTED UDP socket with a real peer and a real state, so this is
    /// a genuine decision to drop them rather than a fixture that happens to have none.
    /// </summary>
    [Fact]
    public void GetUdp_ReportsConnectionlessRowsEvenForAConnectedSocket() {
        var rows = Interop().GetUdp();

        var connected = rows.Single(r => r.LocalPort == 59317);
        Assert.Equal("0.0.0.0", connected.RemoteAddress.ToString());
        Assert.Equal(0, connected.RemotePort);
        Assert.Equal(0u, connected.State);
    }

    [Fact]
    public void GetUdp_IncludesBothAddressFamilies() {
        var rows = Interop().GetUdp();

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.Equal("UDP", r.Protocol));
        Assert.Contains(rows, r => r.LocalAddress.ToString() == "fe80::a00:27ff:fe4e:66a1");
    }

    /// <summary>A host with no <c>/proc/net</c> — or a reader denied it — yields no connections rather than
    /// throwing, the same contract the Windows reader honours when the native call fails.</summary>
    [Fact]
    public void MissingTables_YieldNoConnections() {
        var interop = new LinuxConnectionsInterop(new FakeProcFileSystem());

        Assert.Empty(interop.GetTcp());
        Assert.Empty(interop.GetUdp());
    }

    /// <summary>Only one address family present is the ordinary case on a host with IPv6 disabled.</summary>
    [Fact]
    public void MissingIpv6Table_StillReportsIpv4() {
        var proc = new FakeProcFileSystem().WithFile("/proc/net/tcp", ProcFixtures.ProcNetTcp);

        Assert.Equal(4, new LinuxConnectionsInterop(proc).GetTcp().Count);
    }

    // ----- Reader identity -----

    /// <summary>The arm a green Windows run never executes. Grep the suite for
    /// <c>OperatingSystem.IsLinux</c> after touching any factory — this is why.</summary>
    /// <summary>The Linux arm's construction path, exercised from ANY host — the assertion above only runs
    /// it on Linux, so this is what proves it is sound before CI says so. The real filesystem finds no
    /// <c>/proc/net</c> on a Windows box, which is the same empty contract a denied read produces.</summary>
    [Fact]
    public void LinuxReader_ConstructsAndDegradesOnAnyHost() {
        var interop = new LinuxConnectionsInterop();

        if (!OperatingSystem.IsLinux()) {
            Assert.Empty(interop.GetTcp());
            Assert.Empty(interop.GetUdp());
        }
    }

    [Fact]
    public void ForCurrentPlatform_PicksThisPlatformsReader() {
        var interop = IConnectionsInterop.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsConnectionsInterop>(interop);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxConnectionsInterop>(interop);
        else
            Assert.IsType<UnsupportedConnectionsInterop>(interop);
    }
}
