using DashDetective.Services.Network;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Suggested values for the parameterised rows. Only a convenience — the box stays editable, and
/// nothing here decides what actually runs.
///
/// The lookup itself belongs to <see cref="NetworkGateway"/> (shared with the Network tab's ping panel);
/// what stays here is the Toolkit's own answer to "no gateway", which is a literal host rather than an
/// empty box, because a `ping <host>` row with nothing in it is a dead button.
/// </summary>
public static class ToolkitDefaults {
    /// <summary>Used when there is no gateway to suggest — no adapter, airplane mode, or a read that
    /// failed.</summary>
    public const string FallbackHost = "8.8.8.8";

    /// <summary>The primary adapter's IPv4 default gateway, or <see cref="FallbackHost"/>. Never throws.</summary>
    public static string PrimaryGateway() => NetworkGateway.Primary() ?? FallbackHost;
}
