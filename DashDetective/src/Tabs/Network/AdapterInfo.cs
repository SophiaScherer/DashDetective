using Avalonia.Media;

namespace DashDetective.Tabs.Network;

/// <summary>How an adapter is classified for the status dot, mirroring the design comp: a live
/// physical NIC (green), a live virtual/host adapter (blue), or a down adapter (grey).</summary>
public enum AdapterKind {
    Connected,
    Virtual,
    Disconnected,
}

/// <summary>
/// One network adapter for the Adapters panel: friendly name, hardware description, a status label
/// and link speed, plus its <see cref="AdapterKind"/>. Display-ready strings with "—" fallbacks.
/// The status-dot colours are fixed (not themed) — like the shell's Live/Paused dots and the file
/// type glyphs — so they stay meaningful across accents.
/// </summary>
public sealed record AdapterInfo(
    string Name, string Description, string StatusText, string SpeedText, AdapterKind Kind) {
    private static readonly IBrush ConnectedDot = new SolidColorBrush(Color.Parse("#6ccb5f"));
    private static readonly IBrush VirtualDot = new SolidColorBrush(Color.Parse("#4cc2ff"));
    private static readonly IBrush DisconnectedDot = new SolidColorBrush(Color.Parse("#9aa0a6"));

    /// <summary>The status-dot brush for this adapter's kind (green/blue/grey).</summary>
    public IBrush DotBrush => Kind switch {
        AdapterKind.Connected => ConnectedDot,
        AdapterKind.Virtual => VirtualDot,
        _ => DisconnectedDot,
    };

    /// <summary>Status text colour: greened for a live adapter, muted for a down one. Also fixed.</summary>
    public IBrush StatusBrush => Kind == AdapterKind.Disconnected ? DisconnectedDot : ConnectedDot;
}
