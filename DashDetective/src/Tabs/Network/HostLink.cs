using System;

namespace DashDetective.Tabs.Network;

/// <summary>
/// The https address a typed probe target means, or null when it is not a usable host — which is what
/// leaves a panel's link button disabled. Pure statics, testable without a UI.
///
/// <c>Uri.CheckHostName</c> decides rather than a hand-rolled pattern: it rejects everything that could
/// change what the URL points at. Not <c>ToolkitHostValidator</c>, whose rule is about command-line flags.
/// </summary>
public static class HostLink {
    /// <summary>The https URL for <paramref name="host"/>, or null when it is not a usable host.</summary>
    public static string? For(string? host) {
        var trimmed = host?.Trim() ?? "";
        if (trimmed.Length == 0)
            return null;

        // A bare IPv6 literal is not a legal URL authority — its colons read as a port.
        var authority = Uri.CheckHostName(trimmed) switch {
            UriHostNameType.Dns or UriHostNameType.IPv4 => trimmed,
            UriHostNameType.IPv6 => $"[{trimmed}]",
            _ => null,
        };

        if (authority is null)
            return null;

        return Uri.TryCreate($"https://{authority}", UriKind.Absolute, out var url) ? url.AbsoluteUri : null;
    }
}
