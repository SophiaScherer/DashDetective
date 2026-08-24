using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;

namespace DashDetective.Tabs.Network;

public partial class NetworkView : UserControl {
    /// <summary>How long a revealed adapter row stays tinted before fading back (the fade itself is the
    /// <c>adapterRow</c> style's brush transition).</summary>
    private static readonly TimeSpan HighlightDuration = TimeSpan.FromSeconds(1.6);

    private NetworkViewModel? _subscribed;

    public NetworkView() {
        InitializeComponent();
        // Re-wire the page-changed handler whenever the bound view-model changes, so an explicit pager
        // navigation resets the connections list to the top (the live refresh reconciles in place and
        // deliberately keeps the offset).
        DataContextChanged += (_, _) => Rewire();
    }

    private void Rewire() {
        if (_subscribed is not null) {
            _subscribed.ConnectionsPageChanged -= ScrollConnectionsToTop;
            _subscribed.AdapterRevealRequested -= OnAdapterRevealRequested;
        }

        _subscribed = DataContext as NetworkViewModel;
        if (_subscribed is not null) {
            _subscribed.ConnectionsPageChanged += ScrollConnectionsToTop;
            _subscribed.AdapterRevealRequested += OnAdapterRevealRequested;
        }
    }

    private void ScrollConnectionsToTop() =>
        ConnectionsScroller.Offset = new Vector(ConnectionsScroller.Offset.X, 0);

    /// <summary>
    /// Scrolls the revealed adapter's row into view and flashes it. The Adapters panel has no selection of
    /// its own, so this is a highlight only. Rows are found by the adapter name in their <c>Tag</c>.
    ///
    /// Posted because a reveal arrives in the same breath as the navigation that made this page current.
    /// The name is only taken once a row can be found for it, so a reveal that beat the adapter load is
    /// left pending for the load to re-raise.
    /// </summary>
    private void OnAdapterRevealRequested() =>
        Dispatcher.UIThread.Post(() => {
            if (_subscribed?.TakeRevealedAdapter() is not { } name)
                return;

            if (FindAdapterRow(name) is not { } row)
                return;

            row.BringIntoView();
            Flash(row);
        }, DispatcherPriority.Loaded);

    private Border? FindAdapterRow(string name) =>
        this.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Tag is string tag &&
                                      string.Equals(tag, name, StringComparison.OrdinalIgnoreCase));

    // Tint, then untint on a one-shot timer; the style's transition turns the untint into a fade.
    private static void Flash(Border row) {
        row.Classes.Remove("highlighted");
        row.Classes.Add("highlighted");
        DispatcherTimer.RunOnce(() => row.Classes.Remove("highlighted"), HighlightDuration);
    }
}
