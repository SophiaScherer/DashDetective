using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.ComponentModel;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// The accent picker modal. Embedded by the shell beside the Help overlay rather than inside the Settings
/// page, so its scrim covers the nav bar and the page cannot scroll it away.
///
/// Esc is not handled here: the shell's shortcut chain owns the key and routes it to Cancel.
/// </summary>
public partial class AccentPickerOverlay : UserControl {
    private AccentPickerViewModel? _boundViewModel;

    public AccentPickerOverlay() => InitializeComponent();

    protected override void OnDataContextChanged(System.EventArgs e) {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null)
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _boundViewModel = DataContext as AccentPickerViewModel;

        if (_boundViewModel is not null)
            _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>Takes focus on open, so arrow keys reach the wheel and nothing behind the scrim keeps a
    /// focus ring. Posted because the card is not visible until this layout pass has run.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(AccentPickerViewModel.IsOpen) && _boundViewModel is { IsOpen: true })
            Dispatcher.UIThread.Post(() => Wheel.Focus(), DispatcherPriority.Loaded);
    }

    // A press on the scrim, not the card, dismisses — and only with nothing pending.
    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e) {
        // IsVisualAncestorOf is false for the card itself, which is what an empty spot on it reports.
        if (e.Source is Visual source && (source == Card || Card.IsVisualAncestorOf(source)))
            return;

        _boundViewModel?.DismissFromBackdrop();
    }

    /// <summary>A hex edit ended; the box is tidied back to the draft it produced.</summary>
    private void OnHexLostFocus(object? sender, FocusChangedEventArgs e) => _boundViewModel?.ReconcileHex();

    private void OnHexKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key != Key.Enter)
            return;

        _boundViewModel?.ReconcileHex();
        e.Handled = true;
    }
}
