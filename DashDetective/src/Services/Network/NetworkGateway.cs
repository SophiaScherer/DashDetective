using DashDetective.Services.Diagnostics;
using System;
using System.Net.Sockets;

namespace DashDetective.Services.Network;

/// <summary>
/// The machine's own default gateway — the address a diagnostic should reach for first, and the one
/// target the app can offer without picking someone else's server on the user's behalf.
///
/// Lives here rather than in a tab because two features want it: the Toolkit's parameterised ping/tracert
/// rows and the Network tab's ping panel. It reads through <see cref="NetworkUsageSampler.SelectPrimary"/>
/// so "which adapter is internet-facing" keeps its single owner. Adapter enumeration can be slow, so
/// callers do this off the UI thread.
/// </summary>
internal static class NetworkGateway {
    /// <summary>The primary adapter's IPv4 default gateway, or <c>null</c> when there is none — no
    /// adapter, airplane mode, or a read that failed. Never throws: a suggestion is not worth failing a
    /// page load over. Each caller decides what "no gateway" means, because the answers differ (the
    /// Toolkit falls back to a literal host; the Network tab leaves its box empty rather than inventing
    /// a target the user never asked to contact).</summary>
    public static string? Primary() {
        try {
            var adapter = NetworkUsageSampler.SelectPrimary();
            if (adapter is null)
                return null;

            foreach (var gateway in adapter.GetIPProperties().GatewayAddresses) {
                var address = gateway.Address;

                // IPv4 only: a link-local IPv6 gateway carries a scope id ("fe80::1%12") that the
                // Toolkit's host validator would refuse anyway.
                if (address is not null && address.AddressFamily == AddressFamily.InterNetwork)
                    return address.ToString();
            }
        } catch (Exception error) {
            Log.Warn("Gateway lookup failed", error);
        }

        return null;
    }
}
