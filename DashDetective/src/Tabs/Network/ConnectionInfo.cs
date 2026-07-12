namespace DashDetective.Tabs.Network;

/// <summary>
/// One active connection for the Active Connections table: owning process, endpoints, TCP state
/// (or "—" for UDP) and protocol. Display-ready strings. <see cref="Key"/> is a stable identity for
/// the 4-tuple + owning PID, used to diff snapshots so the list updates in place instead of being
/// rebuilt each poll (which would flicker and lose scroll position).
/// </summary>
public sealed record ConnectionInfo(
    string Process, string LocalEndpoint, string RemoteEndpoint,
    string State, string Protocol, int Pid) {
    public string Key => $"{Protocol}|{LocalEndpoint}|{RemoteEndpoint}|{Pid}";
}
