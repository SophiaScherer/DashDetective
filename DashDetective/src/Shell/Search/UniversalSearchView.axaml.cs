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

    // Whether the top-level pointer listener below is currently attached, so focus arriving twice can't
    // double-subscribe it.
    private bool _watchingForOutsideClick;

    public UniversalSearchView() {
        InitializeComponent();
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
    private void OnSearchBoxGotFocus(object? sender, RoutedEventArgs e) {
        (DataContext as UniversalSearchViewModel)?.NotifyFocused();
        WatchForOutsideClick();
    }

    private void OnSearchBoxLostFocus(object? sender, RoutedEventArgs e) => StopWatchingForOutsideClick();

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

    // The dropdown is deliberately overlay-free (see the XAML), so the "dismiss on outside click" half of
    // light dismiss is re-added here — the same trade, and the same emulation, as the File Explorer's
    // Options menu. It is keyed off focus rather than off the popup being open, because the box can hold
    // the caret with the dropdown shut (after Esc), and that is exactly the state a click away must
    // clear: a text box with focus makes the shell suppress every bare-key shortcut app-wide.
    private void WatchForOutsideClick() {
        if (_watchingForOutsideClick)
            return;

        TopLevel.GetTopLevel(this)?.AddHandler(
            InputElement.PointerPressedEvent, OnWindowPointerPressed,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        _watchingForOutsideClick = true;
    }

    private void StopWatchingForOutsideClick() {
        if (!_watchingForOutsideClick)
            return;

        TopLevel.GetTopLevel(this)?.RemoveHandler(
            InputElement.PointerPressedEvent, OnWindowPointerPressed);
        _watchingForOutsideClick = false;
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.Source is not Visual source || DataContext is not UniversalSearchViewModel vm)
            return;

        // A press on the field itself is the user returning to the box, and a press inside the popup is
        // about to pick a result — neither should cancel anything. The press stays unhandled either way,
        // so it still acts on whatever it landed on.
        if (FieldBorder.IsVisualAncestorOf(source))
            return;
        if (ResultsPopup.Child is Visual child && child.IsVisualAncestorOf(source))
            return;

        // Abandoning the box drops the term as well as the dropdown, and hands the keyboard back — the
        // caret must not be left behind in a field the user has clicked away from.
        vm.Cancel();
        SearchBox.ReleaseFocus();
    }
}
