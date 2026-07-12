using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DashDetective.Tabs.Network;

/// <summary>
/// A row VM for the Active Connections table. Its identity fields (process, endpoints, protocol) are
/// fixed for a given <see cref="Key"/>, so only <see cref="State"/> (and its colour) are observable —
/// a connection can change state (Established → Time-wait) while its 4-tuple persists. Rows are
/// reused across polls and updated in place via <see cref="Update"/>, so the list doesn't flicker.
/// State colours are fixed (not themed), matching the comp and the app's other status indicators.
/// </summary>
public partial class ConnectionRow : ObservableObject {
    private static readonly IBrush EstablishedBrush = new SolidColorBrush(Color.Parse("#6ccb5f"));
    private static readonly IBrush ListeningBrush = new SolidColorBrush(Color.Parse("#4cc2ff"));
    private static readonly IBrush TimeWaitBrush = new SolidColorBrush(Color.Parse("#ffcf4d"));
    private static readonly IBrush OtherBrush = new SolidColorBrush(Color.Parse("#9aa0a6"));

    public ConnectionRow(ConnectionInfo info) {
        Key = info.Key;
        Process = info.Process;
        RemoteEndpoint = info.RemoteEndpoint;
        Protocol = info.Protocol;
        _state = info.State;
        _stateBrush = BrushFor(info.State);
    }

    public string Key { get; }
    public string Process { get; }
    public string RemoteEndpoint { get; }
    public string Protocol { get; }

    [ObservableProperty] private string _state;
    [ObservableProperty] private IBrush _stateBrush;

    /// <summary>Refreshes the mutable fields from a newer snapshot of the same connection.</summary>
    public void Update(ConnectionInfo info) {
        State = info.State;
        StateBrush = BrushFor(info.State);
    }

    private static IBrush BrushFor(string state) => state switch {
        "Established" => EstablishedBrush,
        "Listening" => ListeningBrush,
        "Time-wait" => TimeWaitBrush,
        _ => OtherBrush,
    };
}
