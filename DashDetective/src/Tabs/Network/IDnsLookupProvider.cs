using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>The DNS lookup result: the console body (name + resolved addresses) and a footer line
/// (timing + record type), or a failure note.</summary>
public sealed record DnsResult(string Console, string Footer);

/// <summary>Resolves a user-supplied host for the DNS panel. Implementations must never throw:
/// failure (or a blank host) yields a "could not resolve" note.</summary>
internal interface IDnsLookupProvider {
    Task<DnsResult> GetAsync(string host);
}
