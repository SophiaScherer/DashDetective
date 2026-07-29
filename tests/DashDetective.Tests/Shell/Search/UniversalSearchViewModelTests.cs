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
                    SearchCategory.Page, title, "", 500, () => _onActivate?.Invoke()));

            return Task.FromResult<IReadOnlyList<SearchResult>>(results);
        }
    }

    private static (UniversalSearchViewModel Vm, FakeUiTimer Timer, StubProvider Provider) Build(
        Action? onActivate = null, params string[] titles) {
        var provider = new StubProvider(onActivate, titles.Length > 0 ? titles : ["one", "two", "three"]);
        var timer = new FakeUiTimer();
        return (new UniversalSearchViewModel([provider], timer), timer, provider);
    }

    // Types a term and lets the debounce elapse, as the dispatcher timer would.
    private static async Task SearchAsync(UniversalSearchViewModel vm, FakeUiTimer timer, string term) {
        vm.Text = term;
        timer.RaiseTick();
        await vm.InFlightQuery;
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
        var vm = new UniversalSearchViewModel([provider], timer);

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
        Assert.Empty(vm.Results);
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
        Assert.False(vm.HandleShortcut(ShortcutId.AcceptCompletion));
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
    public void Focus_LeavesTheDropdownShutForAnEmptyBox() {
        var (vm, _, _) = Build();

        vm.Focus();

        Assert.False(vm.IsOpen);
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
