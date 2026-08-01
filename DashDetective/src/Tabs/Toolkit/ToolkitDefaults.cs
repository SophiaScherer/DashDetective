using DashDetective.Services.Diagnostics;
using DashDetective.Services.Network;
using System;
using System.Net.Sockets;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Suggested values for the parameterised rows. Only a convenience — the box stays editable, and
/// nothing here decides what actually runs.
///
/// The gateway is read through <see cref="NetworkUsageSampler.SelectPrimary"/> so "which adapter is
/// internet-facing" keeps its single owner (the Performance tab and the device inventory reuse it the
/// same way). Adapter enumeration can be slow, so callers do this off the UI thread.
/// </summary>
public static class ToolkitDefaults {
    /// <summary>Used when there is no gateway to suggest — no adapter, airplane mode, or a read that
    /// failed. Matches the Network tab's own default ping target; kept as a literal rather than a
    /// reference to it, so the Toolkit does not depend on another tab.</summary>
    public const string FallbackHost = "8.8.8.8";

    /// <summary>The primary adapter's IPv4 default gateway, or <see cref="FallbackHost"/>. Never throws
    /// — a suggestion is not worth failing a page load over.</summary>
    public static string PrimaryGateway() {
        try {
            var adapter = NetworkUsageSampler.SelectPrimary();
            if (adapter is null)
                return FallbackHost;

            foreach (var gateway in adapter.GetIPProperties().GatewayAddresses) {
                var address = gateway.Address;

                // IPv4 only: the box is a convenience, and a link-local IPv6 gateway carries a scope id
                // ("fe80::1%12") that would not survive the host validator anyway.
                if (address is not null && address.AddressFamily == AddressFamily.InterNetwork)
                    return address.ToString();
            }
        } catch (Exception error) {
            Log.Warn("Toolkit gateway lookup failed", error);
        }

        return FallbackHost;
    }
}
