using System;
using System.Runtime.Versioning;

namespace DashDetective.Tabs.Network;

/// <summary>
/// The Network tab's three OS reads, resolved once in the tab's own constructor — the
/// <c>MetricSamplers</c> shape. Only the connection tables are platform-specific, so
/// <see cref="ForCurrentPlatform"/> picks an interop and the portable providers wrap whatever they are
/// handed.
///
/// <b>Single-consumer by design:</b> <see cref="ConnectionsProvider"/> carries a PID→name cache, which
/// is safe only because exactly one <see cref="NetworkViewModel"/> exists. Do not share an instance of
/// this record across pages.
/// </summary>
internal sealed record NetworkProviders(
    IAdapterInfoProvider Adapters, IConnectionsProvider Connections, IDnsLookupProvider Dns) {

    /// <summary>The provider set for this machine. The adapter and DNS readers are portable; only the
    /// connection tables differ, and which one is <see cref="IConnectionsInterop"/>'s own choice.</summary>
    public static NetworkProviders ForCurrentPlatform() =>
        Create(IConnectionsInterop.ForCurrentPlatform());

    private static NetworkProviders Create(IConnectionsInterop interop) => new(
        new AdapterInfoProvider(), new ConnectionsProvider(interop), new DnsLookupProvider());
}
