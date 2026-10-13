using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>The machine's addresses as the internet sees them. A family that could not be read is
/// <c>null</c>, which is a normal outcome (IPv6 on a v4-only network), not an error.</summary>
internal sealed record PublicAddresses(string? Ipv4, string? Ipv6) {
    /// <summary>Neither family answered.</summary>
    public static PublicAddresses None { get; } = new(null, null);
}

/// <summary>
/// Asks an outside service for the machine's public IPv4 and IPv6 addresses. This is the one read in
/// the Network tab that leaves the machine, so callers run it only when the user asks. Implementations
/// must never throw: a family that fails or times out comes back <c>null</c>.
/// </summary>
internal interface IPublicAddressProvider {
    Task<PublicAddresses> GetAsync(CancellationToken token = default);
}
