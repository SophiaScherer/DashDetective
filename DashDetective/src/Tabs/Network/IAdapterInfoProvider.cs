using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>The Adapters + IP Configuration snapshot: every adapter (for the list) plus the primary
/// adapter's IPv4 configuration (for the IP panel).</summary>
public sealed record AdapterSnapshot(IReadOnlyList<AdapterInfo> Adapters, IpConfigInfo PrimaryConfig);

/// <summary>Reads the machine's network adapters and the primary adapter's IP configuration.
/// Implementations must never throw: each adapter and each field falls back independently, so one
/// dead source can't blank the panel.</summary>
internal interface IAdapterInfoProvider {
    Task<AdapterSnapshot> GetAsync(CancellationToken token = default);
}
