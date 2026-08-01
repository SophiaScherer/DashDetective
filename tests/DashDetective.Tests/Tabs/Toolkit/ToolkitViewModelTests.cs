using DashDetective.Tabs.Toolkit;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Covers <see cref="ToolkitViewModel"/>'s wiring: the filter drives the sections and the count, and
/// running a row lands one stanza in the Execution Log, newest first, whether the command worked or not.
///
/// The rows exercised here deliberately point at a tool that does not exist, so the whole
/// view-model → runner → launcher chain runs for real without anything being started.
/// </summary>
public class ToolkitViewModelTests {
    private static ToolkitEntry MissingTool(string command = "not-a-real-tool-xyz") =>
        new(command, "Fails on purpose", ToolkitCategory.SystemTools, ToolkitEntryKind.App,
            ToolkitAction.Launch("not-a-real-tool-xyz.exe"));

    [Fact]
    public void Constructor_ShowsTheAuthoredCommandsWithAllSelected() {
        var vm = new ToolkitViewModel();

        Assert.True(vm.HasCommands);
        Assert.NotEmpty(vm.Groups);
        Assert.True(vm.Categories[0].IsSelected);
    }

    [Fact]
    public void Constructor_StartsWithAnEmptyLog() {
        var vm = new ToolkitViewModel();

        Assert.Empty(vm.Log);
        Assert.False(vm.HasLog);
    }

    [Fact]
    public void Search_NarrowsTheSectionsAndTheCount() {
        var vm = new ToolkitViewModel { Search = "appdata" };

        var shown = vm.Groups.SelectMany(g => g.Items).ToList();

        Assert.NotEmpty(shown);
        Assert.All(shown, entry => Assert.Contains("appdata", entry.Command + entry.Description,
                                                   System.StringComparison.OrdinalIgnoreCase));
        Assert.Equal(shown.Count == 1 ? "1 command" : $"{shown.Count} commands", vm.CountLabel);
    }

    [Fact]
    public void Search_MatchingNothing_LeavesTheEmptyState() {
        var vm = new ToolkitViewModel { Search = "zzzz-no-such-command" };

        Assert.False(vm.HasCommands);
        Assert.Empty(vm.Groups);
    }

    [Fact]
    public async Task Run_RecordsOneStanzaCarryingTheCommandAndTheOutcome() {
        var vm = new ToolkitViewModel();
        var entry = MissingTool();

        await vm.RunCommand.ExecuteAsync(entry);

        var logged = Assert.Single(vm.Log);
        Assert.Equal(entry.Command, logged.Command);
        Assert.False(string.IsNullOrWhiteSpace(logged.Output));
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}$", logged.Time);
        Assert.True(vm.HasLog);
    }

    /// <summary>The log reads as a transcript with the latest run at the top, which is the order the
    /// panel renders in — index 0 is the first stanza on screen.</summary>
    [Fact]
    public async Task Run_Twice_PutsTheNewerRunFirst() {
        var vm = new ToolkitViewModel();

        await vm.RunCommand.ExecuteAsync(MissingTool("first"));
        await vm.RunCommand.ExecuteAsync(MissingTool("second"));

        Assert.Equal(["second", "first"], vm.Log.Select(l => l.Command));
    }

    [Fact]
    public async Task Run_NoEntry_DoesNothing() {
        var vm = new ToolkitViewModel();

        await vm.RunCommand.ExecuteAsync(null);

        Assert.Empty(vm.Log);
    }

    /// <summary>The stanza appears before the command runs, so a slow capture reads as in-flight, and
    /// the result replaces it rather than adding a second stanza.</summary>
    [Fact]
    public async Task Run_ShowsAPendingStanzaThenReplacesItInPlace() {
        var vm = new ToolkitViewModel();
        var entry = MissingTool();

        var running = vm.RunCommand.ExecuteAsync(entry);

        // The placeholder is written synchronously, before the first await inside the runner yields.
        var pending = Assert.Single(vm.Log);
        Assert.Equal(ToolkitOutputFormatter.Running, pending.Output);

        await running;

        var finished = Assert.Single(vm.Log);
        Assert.NotEqual(ToolkitOutputFormatter.Running, finished.Output);
        Assert.Equal(pending.Time, finished.Time);
        Assert.Equal(entry.Command, finished.Command);
    }

    /// <summary>Clearing the log mid-run is an instruction, not a race: the result must not reappear in
    /// a log the user just emptied.</summary>
    [Fact]
    public async Task Run_LogClearedWhileInFlight_DropsTheResult() {
        var vm = new ToolkitViewModel();

        var running = vm.RunCommand.ExecuteAsync(MissingTool());
        vm.ClearLogCommand.Execute(null);
        await running;

        Assert.Empty(vm.Log);
        Assert.False(vm.HasLog);
    }

    /// <summary>Refusing concurrent runs is the page's busy state: every row's button reports
    /// CanExecute false for the duration, so there is no separate flag to drift out of step.</summary>
    [Fact]
    public async Task Run_WhileOneIsInFlight_TheRowsReportThemselvesUnavailable() {
        var vm = new ToolkitViewModel();

        var running = vm.RunCommand.ExecuteAsync(MissingTool());
        Assert.False(vm.RunCommand.CanExecute(MissingTool()));

        await running;

        Assert.True(vm.RunCommand.CanExecute(MissingTool()));
    }

    /// <summary>The stanza keeps the time the command <b>started</b>: for a long capture, stamping it on
    /// completion would put a time in the log that is well after the click that caused it.</summary>
    [Fact]
    public async Task Run_TimestampsTheStartNotTheFinish() {
        var vm = new ToolkitViewModel();

        var running = vm.RunCommand.ExecuteAsync(MissingTool());
        var startedAt = vm.Log[0].Time;
        await running;

        Assert.Equal(startedAt, vm.Log[0].Time);
    }

    [Fact]
    public async Task ClearLog_EmptiesItAndDisablesItsButton() {
        var vm = new ToolkitViewModel();
        await vm.RunCommand.ExecuteAsync(MissingTool());

        vm.ClearLogCommand.Execute(null);

        Assert.Empty(vm.Log);
        Assert.False(vm.HasLog);
    }

    /// <summary>Reveal resets the filter first: a chip or a half-typed search from earlier could
    /// otherwise be hiding the very row universal search just navigated to.</summary>
    [Fact]
    public void Reveal_ClearsTheFilterSoTheRowIsOnThePage() {
        var vm = new ToolkitViewModel { Search = "zzzz-no-such-command" };

        vm.Reveal("%temp%");

        Assert.Equal("", vm.Search);
        Assert.True(vm.HasCommands);
        Assert.Contains(vm.Groups.SelectMany(g => g.Items), entry => entry.Command == "%temp%");
    }
}
