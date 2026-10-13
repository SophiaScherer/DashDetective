using DashDetective.Services.Diagnostics;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>
/// Reads the public addresses from ipify over https: <c>api.ipify.org</c> for IPv4 and
/// <c>api6.ipify.org</c> for IPv6, concurrently, each bounded by its own timeout. A response counts only
/// if it parses as an address of the family asked for, so a captive portal's HTML page reads as
/// unavailable rather than being shown as an address. Never throws.
///
/// Portable managed code — no platform prefix. The <see cref="HttpClient"/> is shared and never
/// disposed per call, which is what keeps sockets from piling up.
/// </summary>
internal sealed class PublicAddressProvider : IPublicAddressProvider {
    internal static readonly Uri Ipv4Endpoint = new("https://api.ipify.org");
    internal static readonly Uri Ipv6Endpoint = new("https://api6.ipify.org");

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(4);

    /// <summary>Longer than any address text (an IPv6 with a scope id is under 60); anything bigger is not one.</summary>
    private const int MaxBodyLength = 64;

    private static readonly HttpClient Shared = new();

    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;

    public PublicAddressProvider() : this(Shared, DefaultTimeout) { }

    /// <summary>Test seam: an explicit client (over a fake handler) and timeout.</summary>
    internal PublicAddressProvider(HttpClient client, TimeSpan timeout) {
        _client = client;
        _timeout = timeout;
    }

    public async Task<PublicAddresses> GetAsync(CancellationToken token = default) {
        var v4 = ReadAsync(Ipv4Endpoint, AddressFamily.InterNetwork, token);
        var v6 = ReadAsync(Ipv6Endpoint, AddressFamily.InterNetworkV6, token);
        return new PublicAddresses(await v4, await v6);
    }

    private async Task<string?> ReadAsync(Uri endpoint, AddressFamily family, CancellationToken token) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(_timeout);
        try {
            using var response = await _client.GetAsync(endpoint, cts.Token);
            response.EnsureSuccessStatusCode();
            var body = (await response.Content.ReadAsStringAsync(cts.Token)).Trim();

            if (body.Length <= MaxBodyLength && IPAddress.TryParse(body, out var address) &&
                address.AddressFamily == family)
                return address.ToString();

            Log.Warn($"Public address lookup at {endpoint.Host} returned something that is not an {family} address.");
            return null;
        } catch (Exception e) when (e is HttpRequestException or OperationCanceledException) {
            // Offline, no route for this family, a refusing server or the timeout. All read as
            // "unavailable", and a v4-only network failing the IPv6 call is the ordinary case.
            Log.Warn($"Public address lookup at {endpoint.Host} failed.", e);
            return null;
        }
    }
}
