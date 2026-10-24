using DashDetective.Shared.Shortcuts;
using DashDetective.Shell.Search;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>Covers <see cref="UniversalSearchViewModel"/>: typing debounces rather than querying per
/// keystroke, the dropdown shows exactly one of results / searching / nothing-found, the arrows wrap,
/// and picking a result runs it and puts the box away.</summary>
public class UniversalSearchViewModelTests {
    /// <summary>Answers with one result per configured title, all equally scored.</summary>
    private sealed class StubProvider : ISearchProvider {
        private readonly string[] _titles;
        private readonly Action? _onActivate;

        public StubProvider(Action? onActivate, params string[] titles) {
            _titles = titles;
            _onActivate = onActivate;
        }

        public int QueryCount { get; private set; }

        public SearchCategory Category => SearchCategory.Page;

        public Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
            QueryCount++;
            var results = new List<SearchResult>();
            foreach (var title in _titles)
                results.Add(new SearchResult(
                    SearchCategory.Page, title, "", 500, () => _onActivate?.Invoke(), Completion: title));

            return Task.FromResult<IReadOnlyList<SearchResult>>(results);
        }
    }

    /// <summary>Holds its answer until released, so a test can type again while a query is running.</summary>
    private sealed class GatedProvider(Action? onActivate = null) : ISearchProvider {
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SearchCategory Category => SearchCategory.Page;

        public void Release() => _released.SetResult();

        public async Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
            await _released.Task;
            return [new SearchResult(SearchCategory.Page, "Notifications", "", 500, () => onActivate?.Invoke(),
                                     Completion: "Notifications")];
        }
    }

    /// <summary>Answers a term with one result titled by it, or with nothing for "nothing", and records
    /// the title of whatever gets activated.</summary>
    private sealed class EchoProvider : ISearchProvider {
        public List<string> Activated { get; } = [];

        public SearchCategory Category => SearchCategory.Page;

        public Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
            var term = query.Term;
            IReadOnlyList<SearchResult> results = term == "nothing"
                ? []
                : [new SearchResult(SearchCategory.Page, term, "", 500, () => Activated.Add(term))];
            return Task.FromResult(results);
        }
    }

    private static (UniversalSearchViewModel Vm, FakeUiTimer Timer, StubProvider Provider) Build(
        Action? onActivate = null, params string[] titles) =>
        BuildWithRecents(new RecentSearches(), onActivate, titles);

    private static (UniversalSearchViewModel Vm, FakeUiTimer Timer, StubProvider Provider) BuildWithRecents(
        RecentSearches recents, Action? onActivate = null, params string[] titles) {
        var provider = new StubProvider(onActivate, titles.Length > 0 ? titles : ["one", "two", "three"]);
        var timer = new FakeUiTimer();
        return (new UniversalSearchViewModel([provider], recents, timer), timer, provider);
    }

    // Types a term and lets the debounce elapse, as the dispatcher timer would.
    private static async Task SearchAsync(UniversalSearchViewModel vm, FakeUiTimer timer, string term) {
        vm.Text = term;
        timer.RaiseTick();
        await vm.InFlightQuery;
    }

    /// <summary>A keystroke landing while a query is still out makes that query's answer stale. It must
    /// not reach the list in the gap before the next debounce starts a fresh one.</summary>
    [Fact]
    public async Task Text_TypedWhileAQueryIsRunning_DiscardsItsAnswer() {
        var provider = new GatedProvider();
        var timer = new FakeUiTimer();
        var vm = new UniversalSearchViewModel([provider], new RecentSearches(), timer);

        vm.Text = "n";
        timer.RaiseTick();
        var stale = vm.InFlightQuery;

        vm.Text = "ne";
        provider.Release();
        await stale;

        Assert.Empty(vm.Results);
        Assert.True(vm.IsSearching);
    }

    [Fact]
    public void Text_DoesNotQueryUntilTheDebounceElapses() {
        var (vm, timer, provider) = Build();

        vm.Text = "net";

        Assert.Equal(0, provider.QueryCount);
        Assert.True(timer.IsRunning);
        Assert.True(vm.IsSearching);
        Assert.True(vm.ShowSearching);
    }

    [Fact]
    public void Text_RestartsTheDebounceOnEveryKeystroke() {
        var (vm, timer, _) = Build();

        vm.Text = "n";
        vm.Text = "ne";
        vm.Text = "net";

        // Each change stops the timer before starting it again, so the pause is measured from the last
        // keystroke rather than the first.
        Assert.Equal(3, timer.StartCount);
        Assert.Equal(3, timer.StopCount);
    }

    [Fact]
    public async Task Text_ShowsTheResultsOnceTheDebounceElapses() {
        var (vm, timer, provider) = Build();

        await SearchAsync(vm, timer, "net");

        Assert.Equal(1, provider.QueryCount);
        Assert.Equal(3, vm.Results.Count);
        Assert.True(vm.IsOpen);
        Assert.True(vm.ShowResultList);
        Assert.False(vm.IsSearching);
        Assert.False(vm.ShowNoResults);
    }

    [Fact]
    public async Task Text_SelectsTheBestMatchSoEnterWorksWithoutArrowing() {
        var (vm, timer, _) = Build();

        await SearchAsync(vm, timer, "net");

        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public async Task Text_ReportsNothingFoundWhenTheProvidersComeBackEmpty() {
        var provider = new StubProvider(null);
        var timer = new FakeUiTimer();
        var vm = new UniversalSearchViewModel([provider], new RecentSearches(), timer);

        await SearchAsync(vm, timer, "zzz");

        Assert.True(vm.ShowNoResults);
        Assert.False(vm.ShowResultList);
        Assert.False(vm.ShowSearching);
    }

    [Fact]
    public async Task Text_ClearingTheBoxClosesTheDropdownWithoutQuerying() {
        var (vm, timer, provider) = Build();
        await SearchAsync(vm, timer, "net");

        vm.Text = "";

        Assert.False(vm.IsOpen);
        Assert.Empty(vm.Results);
        Assert.False(timer.IsRunning);
        Assert.Equal(1, provider.QueryCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Text_TreatsAWhitespaceOnlyTermAsEmpty(string term) {
        var (vm, timer, provider) = Build();

        vm.Text = term;

        Assert.False(vm.IsOpen);
        Assert.False(timer.IsRunning);
        Assert.Equal(0, provider.QueryCount);
    }

    [Fact]
    public async Task Text_GhostsTheBestResultThatExtendsWhatWasTyped() {
        var (vm, timer, _) = Build(null, "Network", "Storage");

        await SearchAsync(vm, timer, "net");

        Assert.Equal("Network", vm.Completion);
    }

    [Fact]
    public async Task Text_SkipsAResultWithNothingToComplete() {
        // "report" matched in the middle of the first name, so it cannot be ghosted onto what is typed;
        // the next result offers the suggestion instead.
        var (vm, timer, _) = Build(null, "quarterly-report.txt", "reporting.md");

        await SearchAsync(vm, timer, "report");

        Assert.Equal("reporting.md", vm.Completion);
    }

    [Fact]
    public async Task Text_KeepsTheGhostAsTypingContinuesTowardsIt() {
        // Between queries the box must not blink the suggestion out and back.
        var (vm, timer, _) = Build(null, "Network");
        await SearchAsync(vm, timer, "net");

        vm.Text = "netw";

        Assert.Equal("Network", vm.Completion);
    }

    [Fact]
    public async Task Text_DropsTheGhostWhenTheBoxIsCleared() {
        var (vm, timer, _) = Build(null, "Network");
        await SearchAsync(vm, timer, "net");

        vm.Text = "";

        Assert.Null(vm.Completion);
    }

    [Fact]
    public async Task Text_GhostsNothingWhenNoResultExtendsTheTerm() {
        var (vm, timer, _) = Build(null, "quarterly-report.txt");

        await SearchAsync(vm, timer, "report");

        Assert.Null(vm.Completion);
    }

    [Fact]
    public async Task MoveSelection_WrapsAtBothEnds() {
        var (vm, timer, _) = Build();
        await SearchAsync(vm, timer, "net");

        vm.MoveSelection(-1);
        Assert.Equal(2, vm.SelectedIndex);

        vm.MoveSelection(1);
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void MoveSelection_DoesNothingWithAnEmptyList() {
        var (vm, _, _) = Build();

        vm.MoveSelection(1);

        Assert.Equal(-1, vm.SelectedIndex);
    }

    [Fact]
    public async Task ActivateSelected_RunsTheResultAndPutsTheBoxAway() {
        var activated = 0;
        var (vm, timer, _) = Build(() => activated++);
        await SearchAsync(vm, timer, "net");

        vm.ActivateSelected();

        Assert.Equal(1, activated);
        Assert.False(vm.IsOpen);
        Assert.Equal("", vm.Text);
    }

    // ----- Enter while the typed term is still pending -----

    /// <summary>The bug: Enter in the same burst as the typing activated the row of the list showing
    /// before the query ran (the recents), not what was typed.</summary>
    [Fact]
    public async Task ActivateSelected_BeforeTheDebounceFires_OpensTheTypedTermNotTheStaleRow() {
        var recents = new RecentSearches();
        recents.Remember(new SearchResult(SearchCategory.Page, "Toolkit", "", 500, () => { }));
        var echo = new EchoProvider();
        var timer = new FakeUiTimer();
        var vm = new UniversalSearchViewModel([echo], recents, timer);
        vm.Focus();
        vm.Text = "storage";

        vm.ActivateSelected();
        await vm.InFlightQuery;

        Assert.Equal(["storage"], echo.Activated);
        Assert.False(timer.IsRunning);
        Assert.False(vm.IsOpen);
        Assert.Equal("", vm.Text);
    }

    [Fact]
    public async Task ActivateSelected_WhileTheQueryIsRunning_OpensItsTopResultWhenItLands() {
        var activated = 0;
        var provider = new GatedProvider(() => activated++);
        var timer = new FakeUiTimer();
        var vm = new UniversalSearchViewModel([provider], new RecentSearches(), timer);
        vm.Text = "n";
        timer.RaiseTick();
        var running = vm.InFlightQuery;

        vm.ActivateSelected();
        Assert.Equal(0, activated);

        provider.Release();
        await running;

        Assert.Equal(1, activated);
    }

    [Fact]
    public async Task Text_TypedAfterAnEarlyEnter_DropsThePendingActivation() {
        var activated = 0;
        var provider = new GatedProvider(() => activated++);
        var timer = new FakeUiTimer();
        var vm = new UniversalSearchViewModel([provider], new RecentSearches(), timer);
        vm.Text = "n";
        vm.ActivateSelected();
        var flushed = vm.InFlightQuery;

        vm.Text = "ne";
        provider.Release();
        await flushed;

        Assert.Equal(0, activated);
    }

    [Fact]
    public async Task Close_AfterAnEarlyEnter_DropsThePendingActivation() {
        var activated = 0;
        var provider = new GatedProvider(() => activated++);
        var vm = new UniversalSearchViewModel([provider], new RecentSearches(), new FakeUiTimer());
        vm.Text = "n";
        vm.ActivateSelected();
        var flushed = vm.InFlightQuery;

        vm.Close();
        provider.Release();
        await flushed;

        Assert.Equal(0, activated);
    }

    [Fact]
    public async Task ActivateSelected_FlushedQueryWithNoResults_ActivatesNothing() {
        var echo = new EchoProvider();
        var vm = new UniversalSearchViewModel([echo], new RecentSearches(), new FakeUiTimer());
        vm.Text = "nothing";

        vm.ActivateSelected();
        await vm.InFlightQuery;

        Assert.Empty(echo.Activated);
        Assert.False(vm.IsSearching);
        Assert.True(vm.ShowNoResults);
        Assert.Equal("nothing", vm.Text);
    }

    [Fact]
    public async Task ActivateSelected_WithSettledResults_DoesNotQueryAgain() {
        var activated = 0;
        var (vm, timer, provider) = Build(() => activated++);
        await SearchAsync(vm, timer, "net");

        vm.ActivateSelected();

        Assert.Equal(1, provider.QueryCount);
        Assert.Equal(1, activated);
    }

    [Fact]
    public async Task Activate_ClickingARowWhileTheTermIsPending_StillOpensThatRow() {
        var recents = new RecentSearches();
        var echo = new EchoProvider();
        var vm = new UniversalSearchViewModel([echo], recents, new FakeUiTimer());
        var clicked = new SearchResult(SearchCategory.Page, "clicked", "", 500, () => echo.Activated.Add("clicked"));
        vm.Text = "storage";

        vm.Activate(clicked);
        await vm.InFlightQuery;

        Assert.Equal(["clicked"], echo.Activated);
    }

    private static (UniversalSearchViewModel Vm, EchoProvider Echo, FakeUiTimer Timer, RecentSearches Recents)
        BuildStale() {
        var recents = new RecentSearches();
        recents.Remember(new SearchResult(SearchCategory.Page, "Toolkit", "", 500, () => { }));
        var echo = new EchoProvider();
        var timer = new FakeUiTimer();
        var vm = new UniversalSearchViewModel([echo], recents, timer);
        vm.Focus();
        return (vm, echo, timer, recents);
    }

    /// <summary>Esc during the debounce left the timer counting and the old list behind, so Enter opened
    /// the stale selected row of a dropdown that was no longer showing.</summary>
    [Fact]
    public async Task Close_DuringTheDebounce_StopsItAndEnterOpensNothing() {
        var (vm, echo, timer, recents) = BuildStale();
        vm.Text = "storage";

        vm.Close();
        Assert.False(timer.IsRunning);
        vm.ActivateSelected();
        await Task.Yield();

        Assert.Empty(echo.Activated);
        // Opening the stale recent would have re-run its search and forgotten it as unmatched.
        Assert.Single(recents.Entries);
    }

    [Fact]
    public async Task Focus_AfterEscDuringTheDebounce_RerunsTheTypedTerm() {
        var (vm, echo, timer, _) = BuildStale();
        vm.Text = "storage";
        vm.Close();

        vm.Focus();
        Assert.True(vm.IsSearching);
        timer.RaiseTick();
        await vm.InFlightQuery;

        Assert.Equal("storage", Assert.Single(vm.Results).Title);
        vm.ActivateSelected();
        Assert.Equal(["storage"], echo.Activated);
    }

    [Fact]
    public async Task Clear_AfterAnEarlyEnter_DropsThePendingActivation() {
        var activated = 0;
        var provider = new GatedProvider(() => activated++);
        var vm = new UniversalSearchViewModel([provider], new RecentSearches(), new FakeUiTimer());
        vm.Text = "n";
        vm.ActivateSelected();
        var flushed = vm.InFlightQuery;

        vm.ClearCommand.Execute(null);
        provider.Release();
        await flushed;

        Assert.Equal(0, activated);
    }

    [Fact]
    public async Task Cancel_AfterAnEarlyEnter_DropsThePendingActivation() {
        var activated = 0;
        var provider = new GatedProvider(() => activated++);
        var vm = new UniversalSearchViewModel([provider], new RecentSearches(), new FakeUiTimer());
        vm.Text = "n";
        vm.ActivateSelected();
        var flushed = vm.InFlightQuery;

        vm.Cancel();
        provider.Release();
        await flushed;

        Assert.Equal(0, activated);
    }

    [Fact]
    public async Task ActivateSelected_FlushedTopResult_IsRemembered() {
        var (vm, _, _, recents) = BuildStale();
        vm.Text = "storage";

        vm.ActivateSelected();
        await vm.InFlightQuery;

        Assert.Equal("storage", recents.Entries[0].Title);
    }

    [Fact]
    public void ActivateSelected_DoesNothingWithNoSelection() {
        var activated = 0;
        var (vm, _, _) = Build(() => activated++);

        vm.ActivateSelected();

        Assert.Equal(0, activated);
    }

    [Fact]
    public async Task HandleShortcut_WalksTheResultsWithTheArrows() {
        var (vm, timer, _) = Build();
        await SearchAsync(vm, timer, "net");

        Assert.True(vm.HandleShortcut(ShortcutId.SelectNextResult));
        Assert.Equal(1, vm.SelectedIndex);

        Assert.True(vm.HandleShortcut(ShortcutId.SelectPreviousResult));
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void HandleShortcut_LeavesTheArrowsAloneWithNothingToWalk() {
        var (vm, _, _) = Build();

        Assert.False(vm.HandleShortcut(ShortcutId.SelectNextResult));
        Assert.False(vm.HandleShortcut(ShortcutId.SelectPreviousResult));
    }

    [Fact]
    public async Task Cancel_DropsTheTermAsWellAsTheDropdown() {
        // Clicking away abandons the search outright, unlike Esc — returning to the box starts fresh
        // rather than resuming something the user walked away from.
        var (vm, timer, _) = Build();
        await SearchAsync(vm, timer, "net");

        vm.Cancel();

        Assert.False(vm.IsOpen);
        Assert.Equal("", vm.Text);
        Assert.Null(vm.Completion);
    }

    [Fact]
    public async Task Cancel_LeavesTheRecentsReadyForTheNextVisit() {
        var recents = new RecentSearches();
        recents.Remember(new SearchResult(SearchCategory.Page, "Network", "Adapters", 500, () => { }));
        var (vm, timer, _) = BuildWithRecents(recents, null, "Storage");
        await SearchAsync(vm, timer, "sto");

        vm.Cancel();
        vm.NotifyFocused();

        Assert.True(vm.IsOpen);
        Assert.Equal("Network", Assert.Single(vm.Results).Title);
    }

    [Fact]
    public void Cancel_IsHarmlessOnAnAlreadyEmptyBox() {
        var (vm, _, _) = Build();

        vm.Cancel();

        Assert.False(vm.IsOpen);
        Assert.Equal("", vm.Text);
    }

    [Fact]
    public async Task HandleShortcut_EscapeClosesTheDropdownButKeepsTheTerm() {
        var (vm, timer, _) = Build();
        await SearchAsync(vm, timer, "net");

        Assert.True(vm.HandleShortcut(ShortcutId.Escape));

        Assert.False(vm.IsOpen);
        Assert.Equal("net", vm.Text);
    }

    [Fact]
    public void HandleShortcut_LeavesEveryOtherShortcutToTheShell() {
        var (vm, _, _) = Build();

        Assert.False(vm.HandleShortcut(ShortcutId.Refresh));
        Assert.False(vm.HandleShortcut(ShortcutId.Export));
    }

    [Fact]
    public async Task Focus_ReopensTheDropdownForATermStillInTheBox() {
        var (vm, timer, _) = Build();
        await SearchAsync(vm, timer, "net");
        vm.Close();

        var focused = 0;
        vm.FocusRequested += () => focused++;
        vm.Focus();

        Assert.True(vm.IsOpen);
        Assert.Equal(1, focused);
    }

    [Fact]
    public void Focus_LeavesTheDropdownShutForAnEmptyBoxWithNoHistory() {
        var (vm, _, _) = Build();

        vm.Focus();

        Assert.False(vm.IsOpen);
    }

    // ----- Recents -----

    [Fact]
    public void Focus_OffersTheRecentsForAnEmptyBox() {
        var recents = new RecentSearches();
        recents.Remember(new SearchResult(SearchCategory.Page, "Network", "Adapters", 500, () => { }));
        var (vm, _, _) = BuildWithRecents(recents);

        vm.Focus();

        Assert.True(vm.IsOpen);
        Assert.Equal("Network", Assert.Single(vm.Results).Title);
    }

    [Fact]
    public void NotifyFocused_OffersTheRecentsWhenTheBoxIsClickedInto() {
        // The regression this guards: recents reached only through Ctrl+F, never with the mouse.
        var recents = new RecentSearches();
        recents.Remember(new SearchResult(SearchCategory.Page, "Network", "Adapters", 500, () => { }));
        var (vm, _, _) = BuildWithRecents(recents);

        vm.NotifyFocused();

        Assert.True(vm.IsOpen);
        Assert.Equal("Network", Assert.Single(vm.Results).Title);
    }

    [Fact]
    public void NotifyFocused_DoesNotAskForFocusItAlreadyHas() {
        var (vm, _, _) = Build();
        var focusRequests = 0;
        vm.FocusRequested += () => focusRequests++;

        vm.NotifyFocused();

        Assert.Equal(0, focusRequests);
    }

    [Fact]
    public async Task NotifyFocused_LeavesAnInFlightSearchAlone() {
        // Clicking back into a box that still holds a term must not replace its results with recents.
        var recents = new RecentSearches();
        recents.Remember(new SearchResult(SearchCategory.Page, "Recent", "", 500, () => { }));
        var (vm, timer, _) = BuildWithRecents(recents, null, "Network");
        await SearchAsync(vm, timer, "net");

        vm.NotifyFocused();

        Assert.Equal("Network", Assert.Single(vm.Results).Title);
    }

    [Fact]
    public async Task ActivateSelected_RemembersWhatWasOpened() {
        var recents = new RecentSearches();
        var (vm, timer, _) = BuildWithRecents(recents, null, "Network");
        await SearchAsync(vm, timer, "net");

        vm.ActivateSelected();

        var entry = Assert.Single(recents.Entries);
        Assert.Equal("Network", entry.Title);
        Assert.Equal(SearchCategory.Page, entry.Category);
    }

    [Fact]
    public async Task ActivateSelected_ReopensTheRecentsOnceTheBoxIsEmptyAgain() {
        var recents = new RecentSearches();
        var (vm, timer, _) = BuildWithRecents(recents, null, "Network");
        await SearchAsync(vm, timer, "net");
        vm.ActivateSelected();

        vm.Focus();

        Assert.True(vm.IsOpen);
        Assert.Equal("Network", Assert.Single(vm.Results).Title);
    }

    // The popup hands focus back to the box while it closes, and the box's GotFocus calls
    // NotifyFocused inside the IsOpen write. This reproduces that re-entrant call.
    private static void RestoreFocusWhenClosing(UniversalSearchViewModel vm) =>
        vm.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(UniversalSearchViewModel.IsOpen) && !vm.IsOpen)
                vm.NotifyFocused();
        };

    /// <summary>Work item 66: focus restored to the box during the close must not re-open the dropdown,
    /// or the view model says open while the popup is shut.</summary>
    [Fact]
    public void Close_FocusRestoredDuringTheClose_DoesNotReopenTheDropdown() {
        var recents = new RecentSearches();
        recents.Remember(new SearchResult(SearchCategory.Page, "Network", "Adapters", 500, () => { }));
        var (vm, _, _) = BuildWithRecents(recents);
        vm.Focus();
        RestoreFocusWhenClosing(vm);

        vm.Close();

        Assert.False(vm.IsOpen);
    }

    /// <summary>Work item 66: a remembered term picked from the dropdown, then a new term typed, must
    /// open the dropdown and deliver results again.</summary>
    [Fact]
    public async Task Text_NewTermAfterPickingARecent_OpensTheDropdownWithResults() {
        var recents = new RecentSearches();
        recents.Remember(new SearchResult(SearchCategory.Page, "Network", "Adapters", 500, () => { }));
        var (vm, timer, _) = BuildWithRecents(recents, null, "Storage");
        vm.Focus();
        RestoreFocusWhenClosing(vm);

        vm.Activate(Assert.Single(vm.Results));
        Assert.False(vm.IsOpen);

        vm.Focus();
        await SearchAsync(vm, timer, "sto");

        Assert.True(vm.IsOpen);
        Assert.Equal("Storage", Assert.Single(vm.Results).Title);
    }

    /// <summary>The popup closing itself reaches the view model through the two-way binding as a plain
    /// write of false; the next term must still be able to open the dropdown.</summary>
    [Fact]
    public async Task IsOpen_SelfClosedByThePopup_ReopensForTheNextTerm() {
        var (vm, timer, _) = Build(null, "Storage");
        await SearchAsync(vm, timer, "sto");
        Assert.True(vm.IsOpen);

        vm.IsOpen = false;
        Assert.False(vm.IsOpen);

        await SearchAsync(vm, timer, "stor");

        Assert.True(vm.IsOpen);
        Assert.Equal("Storage", Assert.Single(vm.Results).Title);
    }

    /// <summary>Only a close in progress ignores the focus; a later click into the box still opens.</summary>
    [Fact]
    public void NotifyFocused_AfterTheCloseHasFinished_OpensAgain() {
        var recents = new RecentSearches();
        recents.Remember(new SearchResult(SearchCategory.Page, "Network", "Adapters", 500, () => { }));
        var (vm, _, _) = BuildWithRecents(recents);
        vm.Focus();
        vm.Close();

        vm.NotifyFocused();

        Assert.True(vm.IsOpen);
    }

    [Fact]
    public async Task Recents_ForgetAnEntryTheSearchNoLongerTurnsUp() {
        // A file deleted, or a process exited, since it was last opened.
        var recents = new RecentSearches();
        recents.Remember(new SearchResult(SearchCategory.Process, "gone.exe", "PID 1", 500, () => { }));
        var (vm, _, _) = BuildWithRecents(recents, null, "Network");

        vm.Focus();
        vm.ActivateSelected();
        await Task.Yield();

        Assert.Empty(recents.Entries);
    }

    [Fact]
    public void Scope_IsTheSearchScope() {
        var (vm, _, _) = Build();

        Assert.Equal(ShortcutScope.Search, vm.Scope);
    }

    [Fact]
    public void Dispose_StopsTheDebounce() {
        var (vm, timer, _) = Build();
        vm.Text = "net";

        vm.Dispose();

        Assert.False(timer.IsRunning);
    }
}
