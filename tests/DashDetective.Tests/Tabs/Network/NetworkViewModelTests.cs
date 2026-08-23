using DashDetective.Tabs.Network;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Network;

/// <summary>Pins that the Network tab reaches the network only when asked. A view model the shell has
/// merely constructed must send no ICMP and resolve no name: both panels used to fire from the
/// constructor, so the app pinged a public resolver and looked up a host from launch, whether or not the
/// tab was ever opened.</summary>
public class NetworkViewModelTests {
    /// <summary>Records every host it was asked to resolve, so a test can assert on "none".</summary>
    private sealed class RecordingDns : IDnsLookupProvider {
        public List<string> Requests { get; } = new();

        public Task<DnsResult> GetAsync(string host) {
            Requests.Add(host);
            return Task.FromResult(new DnsResult($"Name:    {host}", "resolved"));
        }
    }

    private sealed class EmptyAdapters : IAdapterInfoProvider {
        /// <summary>Set to make the read fail, so the page's own soft-fail is reachable.</summary>
        public System.Exception? Fail { get; set; }

        public Task<AdapterSnapshot> GetAsync(CancellationToken token = default) =>
            Fail is { } failure
                ? Task.FromException<AdapterSnapshot>(failure)
                : Task.FromResult(new AdapterSnapshot([], IpConfigInfo.Unknown));
    }

    private sealed class EmptyConnections : IConnectionsProvider {
        /// <summary>Set to make the read fail, so the page's own soft-fail is reachable.</summary>
        public System.Exception? Fail { get; set; }

        public Task<ConnectionsSnapshot> GetAsync(CancellationToken token = default) =>
            Fail is { } failure
                ? Task.FromException<ConnectionsSnapshot>(failure)
                : Task.FromResult(new ConnectionsSnapshot([], 0));
    }

    private static (NetworkViewModel Vm, RecordingDns Dns) Create() {
        var dns = new RecordingDns();
        return (new NetworkViewModel(new NetworkProviders(new EmptyAdapters(), new EmptyConnections(), dns)), dns);
    }

    [Fact]
    public void Construction_ResolvesNothing() {
        var (_, dns) = Create();

        Assert.Empty(dns.Requests);
    }

    [Fact]
    public void LookupDns_IsTheOnlyThingThatResolves() {
        var (vm, dns) = Create();

        vm.LookupDnsCommand.Execute(null);

        Assert.Equal(new[] { vm.DnsHost }, dns.Requests);
    }

    [Fact]
    public void Refresh_BeforeAnyLookup_StillResolvesNothing() {
        var (vm, dns) = Create();

        vm.Refresh();

        Assert.Empty(dns.Requests);
    }

    [Fact]
    public void Refresh_AfterALookup_ReResolvesThatHost() {
        var (vm, dns) = Create();
        vm.LookupDnsCommand.Execute(null);

        vm.Refresh();

        Assert.Equal(2, dns.Requests.Count);
    }

    [Fact]
    public void Construction_LeavesThePingMonitorOff() {
        var (vm, _) = Create();

        Assert.False(vm.PingEnabled);
        Assert.Equal("Start", vm.PingButtonText);
        Assert.Equal("", vm.PingConsole);
    }

    [Fact]
    public async Task TogglePing_WithABlankTarget_StaysOff() {
        var (vm, _) = Create();
        await vm.PingTargetSeeded;   // the gateway suggestion is the field's only other writer
        vm.PingTarget = "";

        vm.TogglePingCommand.Execute(null);

        // Nothing to send, and substituting a host of the app's own choosing is the bug this fixes.
        Assert.False(vm.PingEnabled);
    }

    [Fact]
    public async Task TogglePing_StartsThenStops() {
        var (vm, _) = Create();
        await vm.PingTargetSeeded;
        vm.PingTarget = "192.0.2.1";   // TEST-NET-1: reserved for documentation, so nothing answers

        vm.TogglePingCommand.Execute(null);
        Assert.True(vm.PingEnabled);
        Assert.Equal("Stop", vm.PingButtonText);

        vm.TogglePingCommand.Execute(null);
        Assert.False(vm.PingEnabled);
        Assert.Equal("Start", vm.PingButtonText);
    }

    // ---- Soft-fail ----

    /// <summary>A failed connections read empties the table and says so, rather than leaving the last
    /// snapshot on screen looking current.</summary>
    [Fact]
    public async Task Refresh_WhenTheConnectionsReadFails_SaysSoRatherThanShowingStaleRows() {
        var connections = new EmptyConnections {
            Fail = new System.InvalidOperationException("the TCP table is gone"),
        };
        var vm = new NetworkViewModel(
            new NetworkProviders(new EmptyAdapters(), connections, new RecordingDns()));

        vm.Refresh();
        await Task.Yield();

        Assert.Empty(vm.Connections);
        Assert.False(vm.PagerVisible);
        Assert.Equal("Connections unavailable", vm.ConnectionsSummary);
    }

    /// <summary>A failed adapter read clears the list and drops the IP panel back to Unknown — the panel
    /// must not keep showing an address the machine may no longer hold.</summary>
    [Fact]
    public async Task Refresh_WhenTheAdapterReadFails_ClearsTheAdaptersAndTheIpConfig() {
        var adapters = new EmptyAdapters {
            Fail = new System.InvalidOperationException("the adapter enumeration is gone"),
        };
        var vm = new NetworkViewModel(
            new NetworkProviders(adapters, new EmptyConnections(), new RecordingDns()));

        vm.Refresh();
        await Task.Yield();

        Assert.Empty(vm.Adapters);
        Assert.Same(IpConfigInfo.Unknown, vm.IpConfig);
    }
}
