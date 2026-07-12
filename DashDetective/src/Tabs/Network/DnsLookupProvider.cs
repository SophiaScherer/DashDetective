using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>The DNS lookup result: the console body (name + resolved addresses) and a footer line
/// (timing + record type), or a failure note.</summary>
public sealed record DnsResult(string Console, string Footer);

/// <summary>
/// Resolves a user-supplied host via the in-box <see cref="Dns"/> API, timing the lookup. A one-shot
/// query — run at startup, on toolbar Refresh, and whenever the user submits a new host — not a live
/// loop. A bounded <see cref="CancellationTokenSource"/> caps the wait (an untokened lookup can hang
/// ~10 s when offline). Never throws: failure (or a blank host) yields a "could not resolve" note.
/// </summary>
public static class DnsLookupProvider {
    /// <summary>The default lookup host, used until the user edits the field.</summary>
    public const string DefaultHost = "example.com";

    private const int TimeoutMs = 3000;
    private const int MaxAddresses = 3;

    public static Task<DnsResult> GetAsync(string host) => ResolveAsync(host);

    private static async Task<DnsResult> ResolveAsync(string host) {
        host = host?.Trim() ?? "";
        if (host.Length == 0)
            return Failure(host);

        using var cts = new CancellationTokenSource(TimeoutMs);
        var start = DateTime.UtcNow;
        try {
            var entry = await Dns.GetHostEntryAsync(host, cts.Token);
            var elapsedMs = (long)(DateTime.UtcNow - start).TotalMilliseconds;

            var addresses = entry.AddressList.Take(MaxAddresses).ToList();
            if (addresses.Count == 0)
                return Failure(host);

            var sb = new StringBuilder();
            sb.Append($"Name:    {host}");
            foreach (var address in addresses) {
                sb.Append('\n');
                sb.Append($"Address: {address}");
            }

            var footer = $"Resolved in {elapsedMs.ToString(CultureInfo.InvariantCulture)}ms · " +
                         $"{RecordType(addresses)} record";
            return new DnsResult(sb.ToString(), footer);
        } catch {
            // Timeout (cancelled), SocketException (offline / NXDOMAIN), etc.
            return Failure(host);
        }
    }

    /// <summary>"A" for IPv4, "AAAA" for IPv6, "A + AAAA" when both families are returned.</summary>
    private static string RecordType(System.Collections.Generic.IEnumerable<IPAddress> addresses) {
        var hasV4 = addresses.Any(a => a.AddressFamily == AddressFamily.InterNetwork);
        var hasV6 = addresses.Any(a => a.AddressFamily == AddressFamily.InterNetworkV6);
        if (hasV4 && hasV6) return "A + AAAA";
        return hasV6 ? "AAAA" : "A";
    }

    private static DnsResult Failure(string host) {
        var name = host.Length == 0 ? "(none)" : host;
        return new DnsResult($"Name:    {name}", $"Could not resolve {name}");
    }
}
