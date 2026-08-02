using System;
using System.Net;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Whether a string is something we are willing to hand to <c>ping</c> or <c>tracert</c> as a target.
/// Pure statics, so the rule is testable without a UI or a process.
///
/// This guards the parameterised rows only; what the "+ Add command" form collects is
/// <see cref="ToolkitCommandValidator"/>'s. Injection is already impossible either way — the value becomes
/// one element of a <c>ProcessStartInfo.ArgumentList</c>, never part of a command line (see
/// <see cref="ToolkitAction.WithArgument"/>) — so this is defence in depth with one job of its own: an
/// accepted value **cannot be a flag**. A hostname label may not begin with a hyphen, so <c>-t</c> (ping
/// forever) and friends are rejected before they can change what an authored row does.
/// </summary>
public static class ToolkitHostValidator {
    /// <summary>The longest a DNS name may be, per RFC 1035.</summary>
    public const int MaxLength = 253;

    /// <summary>The longest one dot-separated label may be.</summary>
    public const int MaxLabelLength = 63;

    /// <summary>Whether <paramref name="host"/> is a usable IP literal or DNS name.</summary>
    public static bool IsValid(string? host) {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var trimmed = host.Trim();
        if (trimmed.Length > MaxLength)
            return false;

        // An IPv4 or IPv6 literal is accepted as-is; IPAddress is stricter than anything hand-rolled
        // here would be, and covers the colon-bearing IPv6 forms the label rules below would reject.
        if (IPAddress.TryParse(trimmed, out _))
            return true;

        return IsDnsName(trimmed);
    }

    /// <summary>The value as it should be passed on: trimmed, or empty when it is not usable.</summary>
    public static string Normalize(string? host) =>
        IsValid(host) ? host!.Trim() : "";

    // Letters, digits and hyphens, in dot-separated labels — with a hyphen allowed only *inside* a
    // label. That last rule is what stops the value ever reading as an option.
    private static bool IsDnsName(string host) {
        // A single trailing dot is legal in a fully-qualified name ("example.com.").
        if (host.EndsWith('.'))
            host = host[..^1];

        if (host.Length == 0)
            return false;

        foreach (var label in host.Split('.')) {
            if (label.Length is 0 or > MaxLabelLength)
                return false;

            if (label.StartsWith('-') || label.EndsWith('-'))
                return false;

            foreach (var c in label)
                if (!char.IsAsciiLetterOrDigit(c) && c != '-')
                    return false;
        }

        return true;
    }
}
