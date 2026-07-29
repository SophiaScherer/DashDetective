using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Threading;
using DashDetective.Shared;
using DashDetective.Shared.Completion;
using DashDetective.Shared.Shortcuts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Shell.Search;

/// <summary>
/// The toolbar's universal search box and the dropdown beneath it.
///
/// Typing restarts a short debounce rather than querying per keystroke, so the filesystem is asked once
/// the user pauses instead of once per letter. Each run cancels the one before it, and a late answer for
/// a superseded term is discarded by <see cref="SearchAggregator"/> — the list never flashes results for
/// something the user has already typed past.
///
/// It reports <see cref="ShortcutScope.Search"/> while the dropdown is up, which is what lets the bare
/// arrow keys drive the result list without stealing them from every other page.
/// </summary>
public sealed partial class UniversalSearchViewModel : ViewModelBase, IShortcutTarget, IDisposable {
    /// <summary>How long typing must pause before the providers are asked. Long enough to swallow a
    /// burst of keystrokes, short enough that the list feels attached to the box.</summary>
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(200);

    private readonly SearchAggregator _aggregator;
    private readonly RecentSearches _recents;
    private readonly IUiTimer _debounce;

    // Cancels the in-flight query when the term changes or the box closes.
    private CancellationTokenSource? _running;

    /// <summary>The current results, best first. Grouping is a view concern; the order here is the
    /// order the keyboard walks them in.</summary>
    public ObservableCollection<SearchResult> Results { get; } = new();

    /// <summary>What the user has typed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasText), nameof(ShowNoResults))]
    private string _text = "";

    /// <summary>Which result the keyboard is on, or -1 for none.</summary>
    [ObservableProperty] private int _selectedIndex = -1;

    /// <summary>What Tab should fill the box with — the best result's own name, ghosted after the caret.
    /// Left in place as the user keeps typing: a completion still extending what is typed is still the
    /// right suggestion, so the ghost doesn't blink out and back between queries.</summary>
    [ObservableProperty] private string? _completion;

    /// <summary>Whether the dropdown is showing. Also gates <see cref="Scope"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoResults))]
    private bool _isOpen;

    /// <summary>Whether a query is in flight, so the dropdown can say so instead of showing an empty
    /// list while the filesystem is still answering.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoResults), nameof(ShowSearching))]
    private bool _isSearching;

    /// <summary>Whether the box has anything in it (drives the clear button).</summary>
    public bool HasText => Text.Length > 0;

    // The dropdown shows exactly one of these three at a time: the results, a note that the providers
    // are still working, or a note that they found nothing.

    /// <summary>Whether there are results to list.</summary>
    public bool ShowResultList => Results.Count > 0;

    /// <summary>Whether to say a query is still running — only while there is nothing to show yet, so a
    /// refined search doesn't blank out the results the user is already reading.</summary>
    public bool ShowSearching => IsSearching && Results.Count == 0;

    /// <summary>Whether the dropdown should show its "nothing found" line: open, settled, and empty.</summary>
    public bool ShowNoResults => IsOpen && !IsSearching && Results.Count == 0 && Text.Trim().Length > 0;

    /// <summary>Raised when the search shortcut fires, so the view can put the caret in the box. UI-only;
    /// the same view/view-model seam the Processes filter and the File Explorer path bar use.</summary>
    public event Action? FocusRequested;

    /// <summary>Builds the box over the given providers, on a real dispatcher timer.</summary>
    public UniversalSearchViewModel(IReadOnlyList<ISearchProvider> providers, RecentSearches recents)
        : this(providers, recents, new DispatcherTimerAdapter()) { }

    /// <summary>Test seam: takes the debounce timer explicitly. A real <c>DispatcherTimer</c> only fires
    /// while an Avalonia dispatcher is pumping, so headless tests inject a fake and tick it by hand.</summary>
    internal UniversalSearchViewModel(
        IReadOnlyList<ISearchProvider> providers, RecentSearches recents, IUiTimer debounce) {
        _aggregator = new SearchAggregator(providers);
        _recents = recents;
        _debounce = debounce;
        _debounce.Interval = DebounceDelay;
        _debounce.Tick += OnDebounceElapsed;
    }

    // Every keystroke pushes the query back by restarting the timer. Clearing the box is immediate:
    // there is nothing to search for, so there is nothing to wait for either.
    partial void OnTextChanged(string value) {
        _debounce.Stop();

        if (value.Trim().Length == 0) {
            CancelRunning();
            IsSearching = false;
            Completion = null;
            // Emptying the box goes back to what an empty box shows — the recents, not a blank card.
            ShowRecents();
            return;
        }

        IsSearching = true;
        IsOpen = true;
        _debounce.Start();
    }

    /// <summary>Test seam: the query the last debounce tick started. Production is fire-and-forget (the
    /// results arrive by property change), but a headless test needs something to await.</summary>
    internal Task InFlightQuery { get; private set; } = Task.CompletedTask;

    private void OnDebounceElapsed(object? sender, EventArgs e) {
        _debounce.Stop();
        InFlightQuery = RunQueryAsync(Text);
    }

    private async Task RunQueryAsync(string term) {
        var token = StartRun();

        IReadOnlyList<SearchResult> results;
        try {
            results = await _aggregator.QueryAsync(new SearchQuery(term), token);
        } catch {
            // The aggregator already soft-fails per provider; this is the last net, so a search that
            // goes wrong empties the list rather than taking the shell down.
            results = [];
        }

        // The user typed again while we were waiting, so a newer run owns the list now.
        if (token.IsCancellationRequested)
            return;

        IsSearching = false;
        ShowResults(results);
    }

    // Replaces the list and puts the keyboard on the best match, so Enter works without arrowing first.
    private void ShowResults(IReadOnlyList<SearchResult> results) {
        Results.Clear();
        foreach (var result in results)
            Results.Add(result);

        SelectedIndex = Results.Count > 0 ? 0 : -1;
        UpdateCompletion();
        OnPropertyChanged(nameof(ShowResultList));
        OnPropertyChanged(nameof(ShowSearching));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    // The best result that actually extends what is typed. Usually the first, but a result whose name
    // the term matched in the middle ("report" finding "quarterly-report.txt") has nothing to complete,
    // so the next one down offers the suggestion instead.
    private void UpdateCompletion() {
        foreach (var result in Results)
            if (result.Completion is { } candidate && PrefixCompleter.Suffix(Text, candidate).Length > 0) {
                Completion = candidate;
                return;
            }

        Completion = null;
    }

    /// <summary>Puts the caret in the box (Ctrl+F). An empty box offers the recents; one that still has
    /// a term brings its results back.</summary>
    public void Focus() {
        if (Text.Trim().Length > 0)
            IsOpen = true;
        else
            ShowRecents();

        FocusRequested?.Invoke();
    }

    /// <summary>
    /// Lists the last few things opened from here, so an empty box is a shortcut rather than a blank.
    ///
    /// Each is projected into an ordinary result row, which is what keeps the dropdown a single list
    /// with a single template. Their action re-runs the search rather than replaying a stored one: an
    /// entry pointing at a deleted file or an exited process must not still promise to open it.
    /// </summary>
    private void ShowRecents() {
        var rows = new List<SearchResult>(_recents.Entries.Count);
        foreach (var entry in _recents.Entries) {
            var recent = entry;
            rows.Add(new SearchResult(
                entry.Category, entry.Title, entry.Subtitle, SearchRanker.NoMatch + 1,
                () => _ = ReopenAsync(recent), Key: entry.Key));
        }

        ShowResults(rows);
        IsOpen = rows.Count > 0;
    }

    // Finds the entry again by identity and opens it, or forgets it when the search no longer turns it
    // up — the machine has moved on and the list should too.
    private async Task ReopenAsync(RecentSearch entry) {
        IReadOnlyList<SearchResult> results;
        try {
            results = await _aggregator.QueryAsync(new SearchQuery(entry.Title), CancellationToken.None);
        } catch {
            return;
        }

        foreach (var result in results)
            if (result.Category == entry.Category &&
                string.Equals(result.Identity, entry.Key, StringComparison.OrdinalIgnoreCase)) {
                _recents.Remember(entry);
                result.Activate();
                return;
            }

        _recents.Forget(entry);
    }

    /// <summary>Moves the keyboard <paramref name="delta"/> places through the results, wrapping at both
    /// ends so the arrows cycle rather than sticking.</summary>
    public void MoveSelection(int delta) {
        if (Results.Count == 0)
            return;

        var next = SelectedIndex < 0 ? 0 : (SelectedIndex + delta + Results.Count) % Results.Count;
        SelectedIndex = next;
    }

    /// <summary>Runs the highlighted result and puts the box away. Bound to Enter on the text box itself
    /// (the shortcut layer deliberately leaves Enter to whatever text box has focus).</summary>
    [RelayCommand]
    public void ActivateSelected() {
        if (SelectedIndex >= 0 && SelectedIndex < Results.Count)
            Activate(Results[SelectedIndex]);
    }

    /// <summary>Runs a result the user clicked, wherever it sits in the list.</summary>
    public void Activate(SearchResult result) {
        // A recent's own row re-remembers itself once it has resolved (see ReopenAsync); remembering it
        // here as well would promote an entry that turns out to be dead.
        if (HasText)
            _recents.Remember(result);

        Dismiss();
        result.Activate();
    }

    /// <summary>Empties the box (the × button, and Esc on an already-closed dropdown).</summary>
    [RelayCommand]
    private void Clear() => Text = "";

    /// <summary>Hides the dropdown but leaves the term in place, so Ctrl+F brings the same results
    /// straight back.</summary>
    public void Close() {
        CancelRunning();
        IsSearching = false;
        IsOpen = false;
    }

    // Closing and clearing together: what picking a result should leave behind. Clearing comes first,
    // because emptying the box offers the recents — closing afterwards is what puts the dropdown away.
    private void Dismiss() {
        Text = "";
        Close();
    }

    // ----- Keyboard -----

    /// <summary>Live only while the dropdown is up, which is what keeps the bare arrow keys scrolling
    /// the page everywhere else.</summary>
    public ShortcutScope Scope => ShortcutScope.Search;

    public bool HandleShortcut(ShortcutId id) {
        switch (id) {
            case ShortcutId.SelectNextResult when Results.Count > 0:
                MoveSelection(1);
                return true;

            case ShortcutId.SelectPreviousResult when Results.Count > 0:
                MoveSelection(-1);
                return true;

            // Esc backs out of the dropdown but leaves the term in the box, so Ctrl+F brings the same
            // results straight back. A second press falls through to the page behind it.
            case ShortcutId.Escape:
                Close();
                return true;

            default:
                return false;
        }
    }

    // ----- Query lifetime -----

    // Cancels whatever is in flight and hands back the token for the run replacing it.
    private CancellationToken StartRun() {
        CancelRunning();
        _running = new CancellationTokenSource();
        return _running.Token;
    }

    private void CancelRunning() {
        _running?.Cancel();
        _running?.Dispose();
        _running = null;
    }

    /// <summary>Stops the debounce timer and cancels any in-flight query. Safe to call more than once.</summary>
    public void Dispose() {
        _debounce.Stop();
        _debounce.Tick -= OnDebounceElapsed;
        CancelRunning();
    }
}
