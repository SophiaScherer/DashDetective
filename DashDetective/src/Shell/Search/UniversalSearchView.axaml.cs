using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;

namespace DashDetective.Shell.Search;

public partial class UniversalSearchView : UserControl {
    private UniversalSearchViewModel? _boundViewModel;

    public UniversalSearchView() {
        InitializeComponent();

        // The dropdown is deliberately overlay-free (see the XAML), so the "close on outside click" half
        // of light dismiss is re-added here: a top-level pointer listener, live only while the popup is
        // open, that closes unless the press landed on the field or inside the popup. Same trade — and
        // the same emulation — as the File Explorer's Options menu.
        ResultsPopup.Opened += OnPopupOpened;
        ResultsPopup.Closed += OnPopupClosed;
    }

    // Focus is a view concern, but only the view model knows when the shortcut fired, so it asks.
    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null)
            _boundViewModel.FocusRequested -= OnFocusRequested;

        _boundViewModel = DataContext as UniversalSearchViewModel;

        if (_boundViewModel is not null)
            _boundViewModel.FocusRequested += OnFocusRequested;
    }

    // Selecting the whole term means the next keystroke replaces it — pressing Ctrl+F again is a fresh
    // search, not an append. Posted so focus lands after any binding that reveals the box has applied.
    private void OnFocusRequested() => SearchBox.FocusAndSelectAll();

    // Clicking into the box opens the dropdown too, so the recents are reachable with the mouse and not
    // only through Ctrl+F. Bubbles up from the inner text box, which is what actually takes focus.
    private void OnSearchBoxGotFocus(object? sender, RoutedEventArgs e) =>
        (DataContext as UniversalSearchViewModel)?.NotifyFocused();

    // The same, for a click on a box that already has focus — which is the state the field is left in
    // after a result is picked, and where GotFocus alone leaves the dropdown unreachable by mouse. The
    // press is left unhandled so it still places the caret.
    private void OnFieldPressed(object? sender, PointerPressedEventArgs e) =>
        (DataContext as UniversalSearchViewModel)?.NotifyFocused();

    // The arrows move the selection without the list having focus, so it has to be scrolled by hand.
    private void OnResultsSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        if (sender is ListBox { SelectedItem: { } selected } list)
            list.ScrollIntoView(selected);
    }

    private void OnResultTapped(object? sender, TappedEventArgs e) {
        if (sender is Control { DataContext: SearchResult result } && DataContext is UniversalSearchViewModel vm)
            vm.Activate(result);
    }

    private void OnPopupOpened(object? sender, EventArgs e) =>
        TopLevel.GetTopLevel(this)?.AddHandler(
            InputElement.PointerPressedEvent, OnWindowPointerPressed,
            RoutingStrategies.Tunnel, handledEventsToo: true);

    private void OnPopupClosed(object? sender, EventArgs e) =>
        TopLevel.GetTopLevel(this)?.RemoveHandler(
            InputElement.PointerPressedEvent, OnWindowPointerPressed);

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.Source is not Visual source || DataContext is not UniversalSearchViewModel vm)
            return;

        // A press on the field itself is the user returning to the box, and a press inside the popup is
        // about to pick a result — neither should close anything. The press stays unhandled either way,
        // so it still acts on whatever it landed on.
        if (FieldBorder.IsVisualAncestorOf(source))
            return;
        if (ResultsPopup.Child is Visual child && child.IsVisualAncestorOf(source))
            return;

        vm.Close();
    }
}
