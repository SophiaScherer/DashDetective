using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.ComponentModel;

namespace DashDetective.Shell.Help;

/// <summary>
/// The Help modal: a full-window scrim with a centred card. Embedded directly by the shell (like the
/// navigation bar) rather than routed through the <c>ViewLocator</c>, so it can sit above every other
/// surface including the nav bar.
///
/// The two dismissal gestures live here because they are pure view concerns: a press on the scrim
/// outside the card, and the Esc key. Esc is handled through a tunneling handler on the window while
/// the modal is open — the same idiom the File Explorer uses for its popup — rather than depending on
/// the overlay holding focus.
/// </summary>
public partial class HelpOverlay : UserControl {
    private HelpViewModel? _viewModel;

    // The window the Esc handler is currently attached to. Held rather than re-resolved on detach:
    // by the time the overlay leaves the visual tree, GetTopLevel already returns null.
    private TopLevel? _escHandlerHost;

    public HelpOverlay() {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => DetachEscHandler();
    }

    private void OnDataContextChanged(object? sender, EventArgs e) {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as HelpViewModel;

        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        SyncEscHandler();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(HelpViewModel.IsOpen))
            SyncEscHandler();
    }

    // A press anywhere on the scrim closes, except one that landed inside the card — otherwise
    // interacting with the modal's own content would dismiss it.
    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.Source is Visual source && Card.IsVisualAncestorOf(source))
            return;

        _viewModel?.Close();
    }

    // The Esc listener is only attached while the modal is open, so it never competes with the rest
    // of the app for the key.
    private void SyncEscHandler() {
        if (_viewModel?.IsOpen == true)
            AttachEscHandler();
        else
            DetachEscHandler();
    }

    private void AttachEscHandler() {
        if (_escHandlerHost is not null || TopLevel.GetTopLevel(this) is not { } top)
            return;

        top.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _escHandlerHost = top;
    }

    private void DetachEscHandler() {
        _escHandlerHost?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _escHandlerHost = null;
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key != Key.Escape)
            return;

        _viewModel?.Close();
        e.Handled = true;
    }
}
