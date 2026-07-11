namespace DashDetective.Tabs.Network;

/// <summary>
/// IPv4 addressing for the primary adapter, shown in the IP Configuration panel. Display-ready
/// strings that fall back to "—" when a value is unavailable (e.g. no primary adapter, or an
/// adapter that refuses <c>GetIPProperties()</c>).
/// </summary>
public sealed record IpConfigInfo(
    string Ipv4, string SubnetMask, string Gateway, string Dns, string Mac, string Dhcp) {
    private const string None = "—";

    /// <summary>Fallback shown when there is no primary adapter (e.g. airplane mode) or the read failed.</summary>
    public static IpConfigInfo Unknown { get; } = new(None, None, None, None, None, None);
}
