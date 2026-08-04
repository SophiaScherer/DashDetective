using DashDetective.Tabs.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Network;

/// <summary>Covers <see cref="ConnectionsProvider"/> through a fake <see cref="IConnectionsInterop"/>:
/// identity de-duplication, the UDP substitutions, the TCP state table, sort order, and the row cap
/// reporting an honest pre-cap total.</summary>
public class ConnectionsProviderTests {
    // PID 4 resolves to "System" from the well-known table, so these rows never touch a real process.
    private const int SystemPid = 4;

    private static RawConnection Tcp(string local, int localPort, string remote, int remotePort,
                                     uint state = 5, int pid = SystemPid) =>
        new("TCP", IPAddress.Parse(local), localPort, IPAddress.Parse(remote), remotePort, state, pid);

    private static RawConnection Udp(string local, int localPort, int pid = SystemPid) =>
        new("UDP", IPAddress.Parse(local), localPort, IPAddress.Any, 0, 0, pid);

    private static Task<ConnectionsSnapshot> SnapshotAsync(
        IReadOnlyList<RawConnection>? tcp = null, IReadOnlyList<RawConnection>? udp = null) =>
        new ConnectionsProvider(new FakeInterop(tcp, udp)).GetAsync();

    /// <summary>Two rows sharing Protocol|Local|Remote|Pid must collapse to one: the UI keys rows by
    /// that identity, and a duplicate breaks the keyed diff with an out-of-range Move.</summary>
    [Fact]
    public async Task GetAsync_DuplicateIdentity_IsDeduped() {
        var row = Udp("127.0.0.1", 53);

        var snapshot = await SnapshotAsync(udp: [row, row]);

        Assert.Single(snapshot.Rows);
        Assert.Equal(1, snapshot.Total);
    }

    /// <summary>UDP is connectionless, so it has no remote endpoint and no state to report.</summary>
    [Fact]
    public async Task GetAsync_UdpRow_ReportsNoRemoteOrState() {
        var snapshot = await SnapshotAsync(udp: [Udp("0.0.0.0", 123)]);

        var row = Assert.Single(snapshot.Rows);
        Assert.Equal("0.0.0.0:123", row.LocalEndpoint);
        Assert.Equal("—", row.RemoteEndpoint);
        Assert.Equal("—", row.State);
    }

    [Theory]
    [InlineData(2u, "Listening")]
    [InlineData(5u, "Established")]
    [InlineData(8u, "Close-wait")]
    [InlineData(11u, "Time-wait")]
    [InlineData(99u, "Unknown")]
    public async Task GetAsync_TcpRow_MapsStateToLabel(uint state, string expected) {
        var snapshot = await SnapshotAsync(tcp: [Tcp("10.0.0.1", 1, "10.0.0.2", 443, state)]);

        Assert.Equal(expected, Assert.Single(snapshot.Rows).State);
    }

    /// <summary>Rows sort by process, then remote endpoint, then local endpoint — so a stable order
    /// survives across polls and the keyed diff stays a no-op when nothing changed.</summary>
    [Fact]
    public async Task GetAsync_SortsByProcessThenRemoteThenLocal() {
        var snapshot = await SnapshotAsync(tcp: [
            Tcp("10.0.0.9", 3, "10.0.0.2", 443),
            Tcp("10.0.0.1", 1, "10.0.0.1", 443),
            Tcp("10.0.0.5", 2, "10.0.0.2", 80),
        ]);

        Assert.Equal(
            ["10.0.0.1:443", "10.0.0.2:443", "10.0.0.2:80"],
            snapshot.Rows.Select(r => r.RemoteEndpoint));
    }

    /// <summary>The cap is a memory backstop, not a display limit: the rows are truncated but Total
    /// still reports what was really there, so the panel's count stays honest.</summary>
    [Fact]
    public async Task GetAsync_OverCap_TruncatesRowsButReportsTrueTotal() {
        var rows = new List<RawConnection>();
        for (var port = 0; port < ConnectionsProvider.MaxRows + 25; port++)
            rows.Add(Udp("127.0.0.1", port));

        var snapshot = await SnapshotAsync(udp: rows);

        Assert.Equal(ConnectionsProvider.MaxRows, snapshot.Rows.Count);
        Assert.Equal(ConnectionsProvider.MaxRows + 25, snapshot.Total);
    }

    /// <summary>A throwing interop is contained: the panel blanks rather than the page faulting.</summary>
    [Fact]
    public async Task GetAsync_InteropThrows_SoftFailsToEmpty() {
        var snapshot = await new ConnectionsProvider(new ThrowingInterop()).GetAsync();

        Assert.Empty(snapshot.Rows);
        Assert.Equal(0, snapshot.Total);
    }

    private sealed class FakeInterop(
        IReadOnlyList<RawConnection>? tcp, IReadOnlyList<RawConnection>? udp) : IConnectionsInterop {
        public IReadOnlyList<RawConnection> GetTcp() => tcp ?? [];
        public IReadOnlyList<RawConnection> GetUdp() => udp ?? [];
    }

    private sealed class ThrowingInterop : IConnectionsInterop {
        public IReadOnlyList<RawConnection> GetTcp() => throw new InvalidOperationException("table gone");
        public IReadOnlyList<RawConnection> GetUdp() => [];
    }
}
