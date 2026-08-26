using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DashDetective.Shared;
using System;
using System.Linq;

namespace DashDetective.Tabs.Storage;

public partial class StorageView : UserControl {
    private StorageViewModel? _boundViewModel;

    public StorageView() {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null)
            _boundViewModel.RevealRequested -= OnRevealRequested;

        _boundViewModel = DataContext as StorageViewModel;

        if (_boundViewModel is not null)
            _boundViewModel.RevealRequested += OnRevealRequested;
    }

    /// <summary>
    /// Scrolls the revealed drive's card into view and flashes it. The view model has already selected it,
    /// so the card is found by the disk number in its <c>Tag</c> — the SettingsView.FindRow arrangement,
    /// which keeps the lookup out of step with nothing.
    ///
    /// Posted because a reveal arrives in the same breath as the navigation that made this page current:
    /// the cards do not exist in the visual tree until that layout pass has run.
    /// </summary>
    private void OnRevealRequested() =>
        Dispatcher.UIThread.Post(() => {
            if (_boundViewModel?.SelectedDrive is not { } drive)
                return;

            if (FindCard(drive.DiskNumber) is not { } card)
                return;

            card.BringIntoView();
            RevealFlash.Flash(card);
        }, DispatcherPriority.Loaded);

    private Border? FindCard(int diskNumber) =>
        this.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Tag is int tag && tag == diskNumber);
}
