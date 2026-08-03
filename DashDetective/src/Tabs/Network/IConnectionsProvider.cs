using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>The connections snapshot: the (capped) rows to display plus the true total before
/// capping, so the panel can report an honest count even when the list is truncated.</summary>
public sealed record ConnectionsSnapshot(IReadOnlyList<ConnectionInfo> Rows, int Total);

/// <summary>Snapshots the machine's active TCP/UDP connections for the Active Connections panel.
/// Implementations must never throw: any failure soft-fails to an empty snapshot.</summary>
internal interface IConnectionsProvider {
    Task<ConnectionsSnapshot> GetAsync();
}
