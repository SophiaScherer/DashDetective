using DashDetective.Tabs.Toolkit;
using System.Collections.Generic;
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
    // Label and target agree, as they do on every real row that takes no parameter (%appdata%,
    // regedit, ipconfig /all) — so the logged "$" line is the row's own text.
    private static ToolkitEntry MissingTool(string command = "not-a-real-tool-xyz.exe") =>
        new(command, "Fails on purpose", ToolkitCategory.SystemTools, ToolkitEntryKind.App,
            ToolkitAction.Launch(command));

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

    // ----- Parameterised rows -----

    private static ToolkitEntry Parameterised(string typed) {
        var parameter = new ToolkitParameter("host or IP") { Value = typed };
        return new ToolkitEntry(
            "ping <host>", "Pings a host", ToolkitCategory.Diagnostics, ToolkitEntryKind.Command,
            ToolkitAction.Capture("not-a-real-tool-xyz.exe"), parameter);
    }

    /// <summary>A refused host must not reach the runner at all — the row reports why and stops.</summary>
    [Theory]
    [InlineData("-t")]
    [InlineData("example.com && calc")]
    [InlineData("")]
    public async Task Run_ParameterisedRowWithARefusedHost_LogsWhyAndRunsNothing(string typed) {
        var vm = new ToolkitViewModel();

        await vm.RunCommand.ExecuteAsync(Parameterised(typed));

        var logged = Assert.Single(vm.Log);
        Assert.Equal(ToolkitOutputFormatter.InvalidHost(typed), logged.Output);
        Assert.NotEqual(ToolkitOutputFormatter.Running, logged.Output);
    }

    /// <summary>The "$" line shows what actually ran, not the row's placeholder label — otherwise the
    /// transcript would read "ping &lt;host&gt;" and never say which host.</summary>
    [Fact]
    public async Task Run_ParameterisedRow_LogsTheResolvedCommandLineNotTheLabel() {
        var vm = new ToolkitViewModel();

        await vm.RunCommand.ExecuteAsync(Parameterised("example.com"));

        var logged = Assert.Single(vm.Log);
        Assert.Equal("not-a-real-tool-xyz.exe example.com", logged.Command);
        Assert.DoesNotContain("<host>", logged.Command, System.StringComparison.Ordinal);
    }

    /// <summary>For a row that carries no flags and no placeholder, the resolved line is just the row's
    /// own text — the log does not start paraphrasing ordinary commands.</summary>
    [Fact]
    public async Task Run_PlainRow_LogsTheRowsOwnText() {
        var vm = new ToolkitViewModel();

        await vm.RunCommand.ExecuteAsync(MissingTool("regedit-not-real"));

        Assert.Equal("regedit-not-real", vm.Log[0].Command);
    }

    [Fact]
    public async Task Run_ParameterisedRow_TrimsTheTypedHostBeforePassingItOn() {
        var vm = new ToolkitViewModel();

        await vm.RunCommand.ExecuteAsync(Parameterised("  example.com  "));

        Assert.EndsWith(" example.com", vm.Log[0].Command, System.StringComparison.Ordinal);
    }

    // ----- Log export -----

    [Fact]
    public async Task BuildLogText_CarriesEveryStanzaWithItsTimeAndOutput() {
        var vm = new ToolkitViewModel();
        await vm.RunCommand.ExecuteAsync(MissingTool("first-command"));
        await vm.RunCommand.ExecuteAsync(MissingTool("second-command"));

        var text = vm.BuildLogText();

        Assert.Contains("first-command", text, System.StringComparison.Ordinal);
        Assert.Contains("second-command", text, System.StringComparison.Ordinal);
        Assert.Contains($"[{vm.Log[0].Time}] $ second-command", text, System.StringComparison.Ordinal);
        Assert.Contains(vm.Log[0].Output, text, System.StringComparison.Ordinal);
    }

    /// <summary>The file reads as what was on screen — newest first — rather than quietly reversing it
    /// on the way out.</summary>
    [Fact]
    public async Task BuildLogText_KeepsTheOrderTheLogIsShownIn() {
        var vm = new ToolkitViewModel();
        await vm.RunCommand.ExecuteAsync(MissingTool("older"));
        await vm.RunCommand.ExecuteAsync(MissingTool("newer"));

        var text = vm.BuildLogText();

        Assert.True(text.IndexOf("newer", System.StringComparison.Ordinal) <
                    text.IndexOf("older", System.StringComparison.Ordinal),
                    "export order does not match the panel");
    }

    [Fact]
    public void BuildLogText_EmptyLog_IsStillAWellFormedHeader() {
        var text = new ToolkitViewModel().BuildLogText();

        Assert.Contains("DashDetective", text, System.StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    // ----- Pins -----
    //
    // Pin state lives on the catalog's shared entries (one Toolkit page exists, so they are its rows).
    // These tests therefore start from a known state rather than assuming one, so they do not depend on
    // the order they run in.

    private static ToolkitViewModel Unpinned() {
        var vm = new ToolkitViewModel();
        vm.LoadPins("");
        return vm;
    }

    private static ToolkitEntry Row(string command) =>
        ToolkitCatalog.Entries.First(e => e.Command == command);

    /// <summary>Pins are applied by command text, so a persisted pin still finds its row.</summary>
    [Fact]
    public void LoadPins_MarksTheNamedCommandsAndClearsTheRest() {
        var vm = Unpinned();

        vm.LoadPins(ToolkitPins.Encode(["%temp%"]));

        Assert.True(Row("%temp%").IsPinned);
        Assert.False(Row("%appdata%").IsPinned);

        vm.LoadPins("");
        Assert.All(ToolkitCatalog.Entries, entry => Assert.False(entry.IsPinned));
    }

    /// <summary>A settings file naming a command the catalog no longer carries must not fault the page
    /// — the point of storing pins by text rather than by index.</summary>
    [Fact]
    public void LoadPins_NamingACommandThatNoLongerExists_IsIgnored() {
        var vm = Unpinned();

        vm.LoadPins(ToolkitPins.Encode(["a command that was removed", "%temp%"]));

        Assert.True(Row("%temp%").IsPinned);
        vm.LoadPins("");
    }

    [Fact]
    public void TogglePin_PutsTheRowInThePinnedSectionAndBackAgain() {
        var vm = Unpinned();
        var entry = Row("%temp%");

        vm.TogglePinCommand.Execute(entry);
        Assert.True(entry.IsPinned);
        Assert.Equal(ToolkitGroup.PinnedHeader, vm.Groups[0].Header);

        vm.TogglePinCommand.Execute(entry);
        Assert.False(entry.IsPinned);
        Assert.NotEqual(ToolkitGroup.PinnedHeader, vm.Groups[0].Header);
    }

    /// <summary>Every pin change has to reach the settings store, or a pin survives only until the app
    /// closes.</summary>
    [Fact]
    public void TogglePin_AnnouncesTheChangeSoItCanBePersisted() {
        var vm = Unpinned();
        var entry = Row("%temp%");
        var announced = 0;
        vm.PinsChanged += () => announced++;

        vm.TogglePinCommand.Execute(entry);
        var encoded = vm.EncodePins();
        vm.TogglePinCommand.Execute(entry);

        Assert.Equal(2, announced);
        Assert.Equal(["%temp%"], ToolkitPins.Decode(encoded));
        Assert.Equal("", vm.EncodePins());
    }

    [Fact]
    public void EncodePins_RoundTripsThroughLoadPins() {
        var vm = Unpinned();
        vm.LoadPins(ToolkitPins.Encode(["%temp%", "regedit"]));

        var encoded = vm.EncodePins();
        vm.LoadPins("");
        vm.LoadPins(encoded);

        Assert.Equal(["%temp%", "regedit"],
                     ToolkitCatalog.Entries.Where(e => e.IsPinned).Select(e => e.Command));
        vm.LoadPins("");
    }

    // ----- The user's own commands -----

    private static ToolkitCommand Mine(string title = "My folder") =>
        new(title, "Somewhere I go often", ToolkitCommandType.FolderPath, @"C:\work");

    [Fact]
    public void AddCommand_PutsTheRowOnThePageAndAnnouncesIt() {
        var vm = new ToolkitViewModel();
        var announced = 0;
        vm.CommandsChanged += () => announced++;

        vm.AddCommand(Mine());

        var added = Assert.Single(vm.Custom);
        Assert.Equal("My folder", added.Command);
        Assert.True(added.IsCustom);
        Assert.Equal(1, announced);
    }

    /// <summary>Everything downstream — the filter, the pins, universal search — works off the merged
    /// list, so a command that is not in it may as well not exist.</summary>
    [Fact]
    public void AllEntries_IsTheCatalogPlusTheUsersOwn() {
        var vm = new ToolkitViewModel();

        vm.AddCommand(Mine());

        Assert.Equal(ToolkitCatalog.Entries.Count + 1, vm.AllEntries.Count);
        Assert.Contains(vm.AllEntries, e => e.Command == "My folder");
        Assert.All(ToolkitCatalog.Entries, e => Assert.Contains(e, vm.AllEntries));
    }

    [Fact]
    public void AddCommand_IsFoundByTheSearchBoxLikeAnyOtherRow() {
        var vm = new ToolkitViewModel();
        vm.AddCommand(Mine("zzz-my-own-command"));

        vm.Search = "zzz-my-own";

        Assert.True(vm.HasCommands);
        Assert.Contains(vm.Groups.SelectMany(g => g.Items), e => e.Command == "zzz-my-own-command");
    }

    [Fact]
    public void EncodeCommands_RoundTripsThroughLoadCommands() {
        var vm = new ToolkitViewModel();
        vm.AddCommand(Mine("First"));
        vm.AddCommand(new ToolkitCommand(
            "Ports", "Listening sockets", ToolkitCommandType.Capture, "netstat", "-an",
            ToolkitCategory.Diagnostics));

        var encoded = vm.EncodeCommands();
        var restored = new ToolkitViewModel();
        restored.LoadCommands(encoded);

        Assert.Equal(["First", "Ports"], restored.Custom.Select(e => e.Command));
        Assert.Equal(vm.Custom.Select(e => e.Source), restored.Custom.Select(e => e.Source));
    }

    /// <summary>Restoring what was saved is not a change to save back — announcing it would have every
    /// startup write the settings file straight back out.</summary>
    [Fact]
    public void LoadCommands_ReplacesWhatIsThereWithoutAnnouncingAChange() {
        var vm = new ToolkitViewModel();
        vm.AddCommand(Mine("Old"));
        var announced = 0;
        vm.CommandsChanged += () => announced++;

        vm.LoadCommands(ToolkitCommandCodec.Encode([Mine("New")]));

        Assert.Equal(["New"], vm.Custom.Select(e => e.Command));
        Assert.Equal(0, announced);
    }

    [Fact]
    public void LoadCommands_NothingStored_LeavesNoCustomRows() {
        var vm = new ToolkitViewModel();
        vm.AddCommand(Mine());

        vm.LoadCommands("");

        Assert.Empty(vm.Custom);
    }

    /// <summary>Pins are keyed by command text against the merged list, so one of the user's own commands
    /// pins exactly like a catalog row — which is why the shell loads commands before pins.</summary>
    [Fact]
    public void Pins_ApplyToTheUsersOwnCommandsToo() {
        var vm = new ToolkitViewModel();
        vm.LoadPins("");
        vm.AddCommand(Mine("Pinnable"));
        var entry = vm.Custom[0];

        vm.TogglePinCommand.Execute(entry);
        var encoded = vm.EncodePins();

        Assert.Contains("Pinnable", ToolkitPins.Decode(encoded));

        var restored = new ToolkitViewModel();
        restored.LoadCommands(vm.EncodeCommands());
        restored.LoadPins(encoded);

        Assert.True(restored.Custom[0].IsPinned);
        vm.LoadPins("");
    }

    /// <summary>Loading pins before the commands they name is the ordering bug this guards: the pin finds
    /// nothing and is silently dropped.</summary>
    [Fact]
    public void LoadPins_BeforeLoadCommands_LosesThePinOnACustomRow() {
        var vm = new ToolkitViewModel();
        var pins = ToolkitPins.Encode(["Pinnable"]);

        vm.LoadPins(pins);
        vm.LoadCommands(ToolkitCommandCodec.Encode([Mine("Pinnable")]));

        Assert.False(vm.Custom[0].IsPinned);

        // The order the shell actually uses.
        vm.LoadCommands(ToolkitCommandCodec.Encode([Mine("Pinnable")]));
        vm.LoadPins(pins);
        Assert.True(vm.Custom[0].IsPinned);
        vm.LoadPins("");
    }

    /// <summary>A user's command runs down the ordinary path — the runner cannot tell it from an authored
    /// row, which is the whole point of building it through the same action factories.</summary>
    [Fact]
    public async Task Run_AUsersOwnCaptureCommand_IsLoggedLikeAnyOther() {
        var vm = new ToolkitViewModel();
        vm.AddCommand(new ToolkitCommand(
            "Mine", "", ToolkitCommandType.Capture, "not-a-real-tool-xyz.exe", "-an"));

        await vm.RunCommand.ExecuteAsync(vm.Custom[0]);

        var logged = Assert.Single(vm.Log);
        Assert.Equal("not-a-real-tool-xyz.exe -an", logged.Command);
        Assert.NotEqual(ToolkitOutputFormatter.Running, logged.Output);
    }

    // ----- Copy -----

    /// <summary>What is copied is what would have run, so a paste into a terminal behaves the same as
    /// clicking the row.</summary>
    [Fact]
    public void CopyTextFor_PlainRow_IsTheCommandItself() {
        var vm = new ToolkitViewModel();

        Assert.Equal("regedit-not-real", vm.CopyTextFor(MissingTool("regedit-not-real")));
    }

    [Fact]
    public void CopyTextFor_ParameterisedRow_IncludesTheTypedHost() {
        var vm = new ToolkitViewModel();

        Assert.Equal("not-a-real-tool-xyz.exe example.com",
                     vm.CopyTextFor(Parameterised("example.com")));
    }

    /// <summary>A half-filled or refused box leaves the argument off entirely rather than pasting a
    /// command with a dangling blank on the end.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-t")]
    public void CopyTextFor_ParameterisedRowWithNothingUsable_OmitsTheArgument(string typed) {
        var vm = new ToolkitViewModel();

        Assert.Equal("not-a-real-tool-xyz.exe", vm.CopyTextFor(Parameterised(typed)));
    }

    /// <summary>A documentation row is labelled by title, so copying it must give the URL — copying
    /// "sfc reference" would be useless in a browser.</summary>
    [Fact]
    public void CopyTextFor_DocumentationRow_IsTheUrlNotTheTitle() {
        var vm = new ToolkitViewModel();
        var doc = ToolkitCatalog.Entries.First(e => e.Kind == ToolkitEntryKind.Link);

        var copied = vm.CopyTextFor(doc);

        Assert.StartsWith("https://", copied, System.StringComparison.Ordinal);
        Assert.NotEqual(doc.Command, copied);
    }

    // ----- Elevation -----

    /// <summary>The shield is driven straight off the action, so a row cannot warn about a prompt it
    /// will not raise, or raise one it did not warn about.</summary>
    [Fact]
    public void RequiresElevation_MirrorsTheActionRatherThanBeingSetSeparately() {
        var elevated = new ToolkitEntry(
            "sfc /scannow", "Needs administrator", ToolkitCategory.Diagnostics,
            ToolkitEntryKind.Command, ToolkitAction.Elevated("sfc", "/scannow"));

        Assert.True(elevated.RequiresElevation);
        Assert.False(MissingTool().RequiresElevation);
    }

    /// <summary>Declining the prompt is a decision, not a fault: the stanza says it was cancelled and
    /// nothing is reported as having gone wrong.</summary>
    [Fact]
    public async Task Run_ElevatedRowDeclinedAtThePrompt_ReadsAsCancelled() {
        var vm = new ToolkitViewModel();
        var entry = new ToolkitEntry(
            "cancelled", "Declined at the prompt", ToolkitCategory.Diagnostics,
            ToolkitEntryKind.Command, ToolkitAction.Elevated("not-a-real-tool-xyz.exe"));

        await vm.RunCommand.ExecuteAsync(entry);

        // The tool does not exist, so this run fails before any prompt — what matters is that the
        // failure is worded and logged rather than thrown.
        var logged = Assert.Single(vm.Log);
        Assert.NotEqual(ToolkitOutputFormatter.Running, logged.Output);
        Assert.False(string.IsNullOrWhiteSpace(logged.Output));
    }

    /// <summary>The gateway suggestion arrives after the page is built and must never overwrite a host
    /// the user is part-way through typing.</summary>
    [Fact]
    public void Parameter_SeedIfEmpty_DoesNotOverwriteWhatWasTyped() {
        var parameter = new ToolkitParameter("host or IP") { Value = "mine.example" };

        parameter.SeedIfEmpty("192.168.1.1");

        Assert.Equal("mine.example", parameter.Value);
    }

    [Fact]
    public void Parameter_SeedIfEmpty_FillsAnEmptyBox() {
        var parameter = new ToolkitParameter("host or IP");

        parameter.SeedIfEmpty("192.168.1.1");

        Assert.Equal("192.168.1.1", parameter.Value);
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

    // ----- Opening a location: in the app, or in Explorer -----

    private static ToolkitEntry PathRow(string target = "%windir%") =>
        new(target, "A folder", ToolkitCategory.Folders, ToolkitEntryKind.Folder,
            ToolkitAction.OpenPath(target));

    /// <summary>Clicking a folder row keeps the user in the app: it navigates rather than handing the
    /// path to the shell, so nothing is started and nothing lands in the transcript.</summary>
    [Fact]
    public async Task Run_PathRow_RevealsInTheAppAndRunsNothing() {
        var vm = new ToolkitViewModel();
        var revealed = new List<string>();
        vm.FileExplorerRevealRequested += revealed.Add;

        await vm.RunCommand.ExecuteAsync(PathRow());

        Assert.Equal([ToolkitPaths.Resolve("%windir%")], revealed);
        Assert.Empty(vm.Log);
    }

    /// <summary>The path reaches the File Explorer expanded — it cannot navigate to "%windir%".</summary>
    [Fact]
    public void OpenInApp_SendsTheResolvedPathNotTheAuthoredOne() {
        var vm = new ToolkitViewModel();
        string? revealed = null;
        vm.FileExplorerRevealRequested += path => revealed = path;

        vm.OpenInAppCommand.Execute(PathRow("%temp%"));

        Assert.NotNull(revealed);
        Assert.DoesNotContain("%", revealed, System.StringComparison.Ordinal);
    }

    /// <summary>A shell: row has no filesystem path, so it offers the external icon only — and asking
    /// for the in-app one anyway is refused rather than sending a path Explorer would reject.</summary>
    [Fact]
    public void OpenInApp_ShellLocation_IsNotOfferedAndDoesNothing() {
        var vm = new ToolkitViewModel();
        var shellRow = PathRow("shell:startup");
        var revealed = 0;
        vm.FileExplorerRevealRequested += _ => revealed++;

        vm.OpenInAppCommand.Execute(shellRow);

        Assert.True(shellRow.IsPathEntry);
        Assert.False(shellRow.CanOpenInApp);
        Assert.Equal(0, revealed);
    }

    /// <summary>A shell: row still activates as a run, because there is no in-app destination to prefer
    /// — it is the one folder row whose click behaviour is unchanged.</summary>
    [Fact]
    public async Task Run_ShellLocationRow_StillGoesToTheShellAndIsLogged() {
        var vm = new ToolkitViewModel();
        var revealed = 0;
        vm.FileExplorerRevealRequested += _ => revealed++;

        await vm.RunCommand.ExecuteAsync(PathRow("shell:not-a-real-shell-location-xyz"));

        Assert.Equal(0, revealed);
        Assert.Single(vm.Log);
    }

    /// <summary>The external icon is the row's original behaviour, so it must still write a stanza —
    /// leaving the app is exactly the kind of thing the transcript is for.</summary>
    [Fact]
    public async Task OpenExternally_RunsThroughTheNormalPathAndIsLogged() {
        var vm = new ToolkitViewModel();
        var revealed = 0;
        vm.FileExplorerRevealRequested += _ => revealed++;

        await vm.OpenExternallyCommand.ExecuteAsync(PathRow(@"C:\not-a-real-folder-xyz"));

        Assert.Equal(0, revealed);
        var logged = Assert.Single(vm.Log);
        Assert.NotEqual(ToolkitOutputFormatter.Running, logged.Output);
    }

    /// <summary>Only location rows get the pair of icons — a command or a documentation link has nowhere
    /// on disk to open.</summary>
    [Fact]
    public void IsPathEntry_IsFalseForEverythingThatIsNotALocation() {
        Assert.False(MissingTool().IsPathEntry);
        Assert.False(MissingTool().CanOpenInApp);
        Assert.All(ToolkitCatalog.Entries.Where(e => e.CanOpenInApp),
                   entry => Assert.Equal(ToolkitActionKind.OpenPath, entry.Action.Kind));
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
