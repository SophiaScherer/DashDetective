using DashDetective.Services.Diagnostics;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Network;

/// <summary>
/// Reads the public addresses from ipify over https: <c>api.ipify.org</c> for IPv4 and
/// <c>api6.ipify.org</c> for IPv6, concurrently and off the UI thread, each bounded by its own timeout.
/// A response counts only if it is a canonical address of the family asked for, so a captive portal's
/// HTML page reads as unavailable rather than being shown as an address. Never throws.
///
/// Portable managed code — no platform prefix. The <see cref="HttpClient"/> is shared and never
/// disposed per call, which is what keeps sockets from piling up.
/// </summary>
internal sealed class PublicAddressProvider : IPublicAddressProvider {
    internal static readonly Uri Ipv4Endpoint = new("https://api.ipify.org");
    internal static readonly Uri Ipv6Endpoint = new("https://api6.ipify.org");

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(4);

    /// <summary>Longer than any address text (an IPv6 with a scope id is under 60); anything bigger is not
    /// one, and is never read past this.</summary>
    private const int MaxBodyLength = 64;

    /// <summary>A pooled connection outlives a VPN or Wi-Fi switch and would report the old address.</summary>
    private static readonly HttpClient Shared = new(new SocketsHttpHandler {
        PooledConnectionLifetime = TimeSpan.FromSeconds(10),
    });

    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;

    public PublicAddressProvider() : this(Shared, DefaultTimeout) { }

    /// <summary>Test seam: an explicit client (over a fake handler) and timeout.</summary>
    internal PublicAddressProvider(HttpClient client, TimeSpan timeout) {
        _client = client;
        _timeout = timeout;
    }

    // Task.Run (without the token, which would turn a pre-cancelled call into a throw): the first send
    // can stall on proxy auto-detection, and that must not happen on the UI thread.
    public Task<PublicAddresses> GetAsync(CancellationToken token = default) =>
        Task.Run(() => ReadBothAsync(token), CancellationToken.None);

    private async Task<PublicAddresses> ReadBothAsync(CancellationToken token) {
        var results = await Task.WhenAll(
            ReadAsync(Ipv4Endpoint, AddressFamily.InterNetwork, token),
            ReadAsync(Ipv6Endpoint, AddressFamily.InterNetworkV6, token));
        return new PublicAddresses(results[0], results[1]);
    }

    private async Task<string?> ReadAsync(Uri endpoint, AddressFamily family, CancellationToken token) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(_timeout);
        try {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.ConnectionClose = true;
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();
            var body = await ReadCappedAsync(response, cts.Token);

            if (body is not null && Canonical(body, family) is { } address)
                return address;

            Log.Warn($"Public address lookup at {endpoint.Host} returned something that is not an {family} address.");
            return null;
        } catch (Exception e) when (e is HttpRequestException or OperationCanceledException) {
            // Offline, no route for this family, a refusing server or the timeout. All read as
            // "unavailable". A v4-only network failing the IPv6 call is the ordinary case, so it is not a warning.
            if (family == AddressFamily.InterNetworkV6)
                Log.Info($"Public address lookup at {endpoint.Host} failed.", e);
            else
                Log.Warn($"Public address lookup at {endpoint.Host} failed.", e);
            return null;
        }
    }

    /// <summary>The body as text, or <c>null</c> if it is longer than <see cref="MaxBodyLength"/>. Reads at
    /// most one byte past the cap, so a hostile or broken server cannot make this buffer more.</summary>
    private static async Task<string?> ReadCappedAsync(HttpResponseMessage response, CancellationToken token) {
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var buffer = new byte[MaxBodyLength + 1];
        var filled = 0;
        while (filled < buffer.Length) {
            var read = await stream.ReadAsync(buffer.AsMemory(filled), token);
            if (read == 0)
                break;
            filled += read;
        }

        return filled > MaxBodyLength ? null : Encoding.ASCII.GetString(buffer, 0, filled).Trim();
    }

    /// <summary><see cref="IPAddress.TryParse(string, out IPAddress)"/> accepts "12345" and "1.2" as IPv4,
    /// so the text must also have the shape of the family: four plain octets, or a colon for IPv6.</summary>
    internal static string? Canonical(string text, AddressFamily family) {
        if (!IPAddress.TryParse(text, out var address) || address.AddressFamily != family)
            return null;

        var shaped = family == AddressFamily.InterNetwork
            ? text.Split('.') is { Length: 4 } octets &&
              octets.All(o => o.Length is >= 1 and <= 3 && o.All(char.IsAsciiDigit))
            : text.Contains(':');
        return shaped ? address.ToString() : null;
    }
}
