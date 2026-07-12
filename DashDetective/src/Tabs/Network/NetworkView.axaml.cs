using Avalonia;
using Avalonia.Controls;

namespace DashDetective.Tabs.Network;

public partial class NetworkView : UserControl {
    private NetworkViewModel? _subscribed;

    public NetworkView() {
        InitializeComponent();
        // Re-wire the page-changed handler whenever the bound view-model changes, so an explicit pager
        // navigation resets the connections list to the top (the live refresh reconciles in place and
        // deliberately keeps the offset).
        DataContextChanged += (_, _) => Rewire();
    }

    private void Rewire() {
        if (_subscribed is not null)
            _subscribed.ConnectionsPageChanged -= ScrollConnectionsToTop;

        _subscribed = DataContext as NetworkViewModel;
        if (_subscribed is not null)
            _subscribed.ConnectionsPageChanged += ScrollConnectionsToTop;
    }

    private void ScrollConnectionsToTop() =>
        ConnectionsScroller.Offset = new Vector(ConnectionsScroller.Offset.X, 0);
}
