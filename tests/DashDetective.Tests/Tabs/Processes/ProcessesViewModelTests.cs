using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Tabs.Processes;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessesViewModel"/> through the <see cref="IProcessSnapshotProvider"/>
/// and <see cref="IProcessInterop"/> seams: a canned snapshot builds the tree and summary counts, and the
/// Properties forwarder reaches the interop with the handle the code-behind supplies.</summary>
public class ProcessesViewModelTests {
    private static ProcessInfo Proc(int pid, int parentPid, string name, ProcessCategory category) =>
        new(pid, parentPid, name, "Running", 0, 0, 1, category, 0, 0);

    private static (ProcessesViewModel ViewModel, FakeProcessInterop Interop) Create(
        params ProcessInfo[] processes) {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var interop = new FakeProcessInterop();
        return (new ProcessesViewModel(metrics, new FakeSnapshotProvider(processes), interop), interop);
    }

    /// <summary>
    /// Leaving the tab mid-read must abandon the load rather than let it land. Without this the read
    /// completes off-screen and, on a failure, writes the emptying fallback — which the user then meets
    /// as a blank list the next time they open the tab.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WhenTheTabIsLeftMidRead_KeepsTheRowsAlreadyOnScreen() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var provider = new ControllableSnapshotProvider([Proc(100, 0, "editor.exe", ProcessCategory.App)]);
        var viewModel = new ProcessesViewModel(metrics, provider, new FakeProcessInterop());

        // The constructor's own load seeds the list.
        await viewModel.LoadAsync();
        Assert.Single(viewModel.Apps);

        // Now arm the provider to park, then fail — and leave the tab while it is parked.
        provider.Gate = new TaskCompletionSource();
        provider.Fail = new InvalidOperationException("the enumeration broke");
        viewModel.SetActive(true);
        var inFlight = viewModel.LoadAsync();

        viewModel.SetActive(false);   // the user switches away
        provider.Gate.SetResult();    // the read comes back badly, into a page nobody is watching
        await inFlight;

        // The soft-fail would have cleared every group. Off screen, it must not have run.
        Assert.Single(viewModel.Apps);
    }

    /// <summary>
    /// The constructor's own load must survive. It runs before the page is ever shown — universal search
    /// reads the snapshot from tabs the user never opens — so it captures the gate's token while the gate
    /// is still idle. Building the gate after that load would make the capture dereference a null field,
    /// and the soft-fail would swallow it as an ordinary failure and empty the page: a blank tab with
    /// nothing logged. Asserting on the constructor alone, with no explicit load, is what pins the order.
    /// </summary>
    [Fact]
    public void Construction_LoadsTheSnapshotWithoutTrippingOverItsOwnGate() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var provider = new ControllableSnapshotProvider([Proc(100, 0, "editor.exe", ProcessCategory.App)]);

        var viewModel = new ProcessesViewModel(metrics, provider, new FakeProcessInterop());

        Assert.Single(viewModel.Apps);
        Assert.Equal("1", viewModel.TotalProcessesText);
    }

    /// <summary>
    /// A dead CPU counter reports no reading, not a confident 0%. This page used to render the failure as
    /// "0%" while the Dashboard, Performance and Network tabs rendered the very same feed as "—", and
    /// <see cref="MetricChannel{T}"/> stops polling after a failure — so that "0%" sat there claiming an
    /// idle machine for the rest of the session.
    /// </summary>
    [Fact]
    public void CpuSamplerFailure_ReportsNoReadingRatherThanAConfidentZero() {
        var timers = new List<FakeUiTimer>();
        var samplers = new MetricSamplers(
            () => throw new InvalidOperationException("the counter is gone"),
            () => new MemorySample(0, 0, 0, 0, 0),
            () => new NetworkSample(0, 0),
            () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => {
            var timer = new FakeUiTimer();
            timers.Add(timer);
            return timer;
        });
        var viewModel = new ProcessesViewModel(
            metrics, new FakeSnapshotProvider([]), new FakeProcessInterop());

        viewModel.SetActive(true);   // attaches the subscriptions
        timers[0].RaiseTick();       // the CPU feed's timer

        Assert.Equal("—", viewModel.CpuUsageText);
    }

    /// <summary>A canned snapshot lands in the three Task-Manager-style groups by category.</summary>
    [Fact]
    public async Task LoadAsync_GroupsProcessesByCategory() {
        var (viewModel, _) = Create(
            Proc(100, 0, "editor.exe", ProcessCategory.App),
            Proc(200, 0, "updater.exe", ProcessCategory.Background),
            Proc(300, 0, "svchost.exe", ProcessCategory.Windows));

        await viewModel.LoadAsync();

        Assert.Equal("editor.exe", Assert.Single(viewModel.Apps).Name);
        Assert.Equal("updater.exe", Assert.Single(viewModel.Background).Name);
        Assert.Equal("svchost.exe", Assert.Single(viewModel.WindowsProcesses).Name);
    }

    /// <summary>An empty snapshot clears the groups rather than leaving the previous poll's rows.</summary>
    [Fact]
    public async Task LoadAsync_EmptySnapshot_ClearsEveryGroup() {
        var (viewModel, _) = Create();

        await viewModel.LoadAsync();

        Assert.Empty(viewModel.Apps);
        Assert.Empty(viewModel.Background);
        Assert.Empty(viewModel.WindowsProcesses);
    }

    /// <summary>The third group's caption and breakdown word come from <see cref="ProcessGroupNames"/>, so
    /// a Linux desktop reads "System processes" rather than "Windows processes". Asserted against the host's
    /// own arm so the test states the wiring, not the platform.</summary>
    [Fact]
    public async Task LoadAsync_CaptionsTheThirdGroupForThisPlatform() {
        var (viewModel, _) = Create(
            Proc(300, 0, "svchost.exe", ProcessCategory.Windows),
            Proc(301, 0, "lsass.exe", ProcessCategory.Windows));

        await viewModel.LoadAsync();

        Assert.Equal($"{ProcessGroupNames.SystemHeader} · 2", viewModel.WindowsHeader);
        Assert.EndsWith($"2 {ProcessGroupNames.SystemLabel}", viewModel.ProcessBreakdownText, StringComparison.Ordinal);
    }

    /// <summary>The failure path resets to the same caption the constructor used, so a soft-failed poll
    /// cannot leave a Linux desktop showing the Windows wording.</summary>
    [Fact]
    public async Task LoadAsync_EmptySnapshot_ResetsToThePlatformCaption() {
        var (viewModel, _) = Create();

        await viewModel.LoadAsync();

        Assert.StartsWith(ProcessGroupNames.SystemHeader, viewModel.WindowsHeader, StringComparison.Ordinal);
    }

    /// <summary>The dialog needs the owning window handle, which only the view can fetch — so the view
    /// model forwards rather than the code-behind reaching an interop directly.</summary>
    [Fact]
    public void ShowProperties_ForwardsHandleAndPidToTheInterop() {
        var (viewModel, interop) = Create();

        viewModel.ShowProperties(new IntPtr(4242), 1234);

        Assert.Equal((new IntPtr(4242), 1234), Assert.Single(interop.Calls));
    }

    // ----- Column order -----

    [Fact]
    public void ColumnOrder_StartsAsDeclared() {
        var (viewModel, _) = Create(Proc(100, 0, "editor.exe", ProcessCategory.App));

        Assert.Equal(ProcessColumns.DefaultOrder, viewModel.ColumnOrder);
        Assert.Equal(0, viewModel.NameColumn);
        Assert.Equal(1, viewModel.PidColumn);
        Assert.Equal(6, viewModel.GpuColumn);
    }

    [Fact]
    public void MoveColumn_MovesTheCellsWithIt() {
        var (viewModel, _) = Create(Proc(100, 0, "editor.exe", ProcessCategory.App));

        Assert.True(viewModel.MoveColumn(ProcessColumnId.Gpu, 1));

        Assert.Equal(1, viewModel.GpuColumn);
        Assert.Equal(2, viewModel.PidColumn);
        Assert.Equal("2.4*,0.85*,0.7*,1*,0.85*,0.85*,0.85*", viewModel.ColumnLayout);
    }

    /// <summary>The drag calls MoveColumn on every pointer move, so a wobble that lands on the column's
    /// current position must not churn bindings.</summary>
    [Fact]
    public void MoveColumn_ToWhereItAlreadyIs_DoesNothing() {
        var (viewModel, _) = Create(Proc(100, 0, "editor.exe", ProcessCategory.App));

        Assert.False(viewModel.MoveColumn(ProcessColumnId.Pid, 1));
    }

    /// <summary>Name owns the tree indent and the chevron, so it neither moves nor is displaced.</summary>
    [Fact]
    public void MoveColumn_LeavesThePinnedColumnAlone() {
        var (viewModel, _) = Create(Proc(100, 0, "editor.exe", ProcessCategory.App));

        Assert.False(viewModel.MoveColumn(ProcessColumnId.Name, 3));
        Assert.True(viewModel.MoveColumn(ProcessColumnId.Cpu, 0));

        Assert.Equal(0, viewModel.NameColumn);
        Assert.Equal(1, viewModel.CpuColumn);
    }

    /// <summary>A drag reports once, on release — not on every pointer move, which would rewrite the
    /// settings file dozens of times for one gesture.</summary>
    [Fact]
    public void MoveColumn_IsSilentUntilTheOrderIsCommitted() {
        var (viewModel, _) = Create(Proc(100, 0, "editor.exe", ProcessCategory.App));
        var reported = 0;
        viewModel.PreferencesChanged += () => reported++;

        viewModel.MoveColumn(ProcessColumnId.Gpu, 1);
        viewModel.MoveColumn(ProcessColumnId.Gpu, 2);
        Assert.Equal(0, reported);

        viewModel.CommitColumnOrder();
        Assert.Equal(1, reported);
    }

    [Fact]
    public void ResetColumnOrder_RestoresTheDeclaredOrderAndReportsIt() {
        var (viewModel, _) = Create(Proc(100, 0, "editor.exe", ProcessCategory.App));
        var reported = 0;
        viewModel.PreferencesChanged += () => reported++;
        viewModel.MoveColumn(ProcessColumnId.Gpu, 1);

        viewModel.ResetColumnOrder();

        Assert.Equal(ProcessColumns.DefaultOrder, viewModel.ColumnOrder);
        Assert.Equal(1, reported);

        // Already default: nothing to report the second time.
        viewModel.ResetColumnOrder();
        Assert.Equal(1, reported);
    }

    [Fact]
    public void ColumnOrder_Assigned_ResolvesAgainstTheColumnsTheTableHasNow() {
        var (viewModel, _) = Create(Proc(100, 0, "editor.exe", ProcessCategory.App));

        // A save from a release that had no Disk or GPU column, with Name written last.
        viewModel.ColumnOrder = new[] {
            ProcessColumnId.Cpu, ProcessColumnId.Pid, ProcessColumnId.Status,
            ProcessColumnId.Memory, ProcessColumnId.Name,
        };

        Assert.Equal(0, viewModel.NameColumn);
        Assert.Equal(1, viewModel.CpuColumn);
        Assert.Equal(2, viewModel.PidColumn);
        // Disk and GPU were never saved, so they keep their declared place after Memory.
        Assert.Equal(5, viewModel.DiskColumn);
        Assert.Equal(6, viewModel.GpuColumn);
    }

    // ----- Selection -----

    private static (ProcessesViewModel ViewModel, ControllableSnapshotProvider Provider) Selectable() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var provider = new ControllableSnapshotProvider([
            Proc(100, 0, "editor.exe", ProcessCategory.App),
            Proc(200, 0, "browser.exe", ProcessCategory.App),
            Proc(300, 0, "helper.exe", ProcessCategory.Background),
            Proc(400, 0, "tray.exe", ProcessCategory.Background),
            Proc(500, 0, "svchost.exe", ProcessCategory.Windows),
        ]);

        return (new ProcessesViewModel(metrics, provider, new FakeProcessInterop()), provider);
    }

    private static ProcessRow Row(ProcessesViewModel viewModel, int pid) =>
        viewModel.Apps.Concat(viewModel.Background).Concat(viewModel.WindowsProcesses)
                 .Single(row => row.Pid == pid);

    [Fact]
    public async Task SelectRow_Plain_ReplacesTheSelection() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();

        viewModel.SelectRow(Row(viewModel, 100));
        viewModel.SelectRow(Row(viewModel, 300));

        Assert.Equal(new[] { 300 }, viewModel.SelectedPids);
        Assert.False(Row(viewModel, 100).IsSelected);
        Assert.True(Row(viewModel, 300).IsSelected);
    }

    [Fact]
    public async Task SelectRow_WithControl_AddsThenRemovesTheOneRow() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();

        viewModel.SelectRow(Row(viewModel, 100));
        viewModel.SelectRow(Row(viewModel, 300), extend: true, range: false);
        Assert.Equal(2, viewModel.SelectionCount);

        viewModel.SelectRow(Row(viewModel, 300), extend: true, range: false);
        Assert.Equal(new[] { 100 }, viewModel.SelectedPids);
    }

    /// <summary>A range reads the way the eye does — down the screen, straight through the group
    /// headings, not per group.</summary>
    [Fact]
    public async Task SelectRow_WithShift_TakesTheRunAcrossGroups() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();

        // browser.exe sorts above editor.exe, so Apps read browser, editor.
        viewModel.SelectRow(Row(viewModel, 100));
        viewModel.SelectRow(Row(viewModel, 400), extend: false, range: true);

        Assert.Equal(new[] { 100, 300, 400 }, viewModel.SelectedPids.OrderBy(pid => pid));
    }

    [Fact]
    public async Task SelectRange_Backwards_SelectsTheSameRun() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();

        viewModel.SelectRange(400, 100);

        Assert.Equal(new[] { 100, 300, 400 }, viewModel.SelectedPids.OrderBy(pid => pid));
    }

    [Fact]
    public async Task SetGroupSelected_TakesAndClearsTheWholeGroup() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();

        viewModel.SetGroupSelected(ProcessCategory.App, selected: true);
        Assert.Equal(new[] { 100, 200 }, viewModel.SelectedPids.OrderBy(pid => pid));
        Assert.True(viewModel.AppsAllSelected);
        Assert.False(viewModel.AppsSomeSelected);
        Assert.False(viewModel.BackgroundAllSelected);

        viewModel.SetGroupSelected(ProcessCategory.App, selected: false);
        Assert.Empty(viewModel.SelectedPids);
    }

    [Fact]
    public async Task GroupState_PartlySelected_ReadsAsSomeNotAll() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();

        viewModel.SelectRow(Row(viewModel, 100));

        Assert.False(viewModel.AppsAllSelected);
        Assert.True(viewModel.AppsSomeSelected);
        Assert.False(viewModel.IsGroupFullySelected(ProcessCategory.App));
    }

    /// <summary>An empty group must not read as "all selected" — its box would be ticked with nothing
    /// under it.</summary>
    [Fact]
    public async Task GroupState_EmptyGroup_ReadsAsNeither() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();

        viewModel.FilterText = "svchost";

        Assert.Empty(viewModel.Apps);
        Assert.False(viewModel.AppsAllSelected);
        Assert.False(viewModel.AppsSomeSelected);
    }

    /// <summary>Rows are recreated by the keyed diff, so the set has to be what survives a poll.</summary>
    [Fact]
    public async Task Selection_SurvivesAPoll() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();
        viewModel.SelectRow(Row(viewModel, 100));
        viewModel.SelectRow(Row(viewModel, 300), extend: true, range: false);

        await viewModel.LoadAsync();

        Assert.Equal(new[] { 100, 300 }, viewModel.SelectedPids.OrderBy(pid => pid));
        Assert.True(Row(viewModel, 100).IsSelected);
        Assert.True(Row(viewModel, 300).IsSelected);
    }

    /// <summary>Narrowing the list is not a reason to lose a process the user picked; only exiting is.</summary>
    [Fact]
    public async Task Selection_SurvivesTheFilterHidingTheRow() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();
        viewModel.SelectRow(Row(viewModel, 100));

        viewModel.FilterText = "svchost";
        Assert.Equal(new[] { 100 }, viewModel.SelectedPids);

        viewModel.FilterText = "";
        Assert.True(Row(viewModel, 100).IsSelected);
    }

    [Fact]
    public async Task Selection_DropsAProcessThatHasExited() {
        var (viewModel, provider) = Selectable();
        await viewModel.LoadAsync();
        viewModel.SelectRow(Row(viewModel, 100));
        viewModel.SelectRow(Row(viewModel, 300), extend: true, range: false);

        provider.Processes = [
            Proc(300, 0, "helper.exe", ProcessCategory.Background),
            Proc(500, 0, "svchost.exe", ProcessCategory.Windows),
        ];
        await viewModel.LoadAsync();

        Assert.Equal(new[] { 300 }, viewModel.SelectedPids);
        Assert.True(viewModel.HasSelection);
    }

    [Fact]
    public async Task ClearSelection_EmptiesEverythingDerivedFromIt() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();
        viewModel.SetGroupSelected(ProcessCategory.App, selected: true);

        viewModel.ClearSelection();

        Assert.False(viewModel.HasSelection);
        Assert.Equal(0, viewModel.SelectionCount);
        Assert.Null(viewModel.SelectedRow);
        Assert.False(Row(viewModel, 100).IsSelected);
    }

    // ----- End task -----

    private static (ProcessesViewModel ViewModel, FakeProcessTerminator Terminator) Endable() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var provider = new ControllableSnapshotProvider([
            Proc(100, 0, "editor.exe", ProcessCategory.App),
            Proc(200, 0, "browser.exe", ProcessCategory.App),
            Proc(300, 0, "helper.exe", ProcessCategory.Background),
            Proc(400, 0, "tray.exe", ProcessCategory.Background),
        ]);
        var terminator = new FakeProcessTerminator();

        return (new ProcessesViewModel(metrics, provider, new FakeProcessInterop(), terminator), terminator);
    }

    [Fact]
    public async Task ConfirmEndTask_EndsEverySelectedProcess() {
        var (viewModel, terminator) = Endable();
        await viewModel.LoadAsync();
        viewModel.SelectRow(Row(viewModel, 100));
        viewModel.SelectRow(Row(viewModel, 300), extend: true, range: false);

        viewModel.RequestEndTaskCommand.Execute(null);
        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.Equal([100, 300], terminator.Ended.OrderBy(pid => pid));
        Assert.Empty(viewModel.SelectedPids);
        Assert.Equal("", viewModel.ActionMessage);
        Assert.DoesNotContain(viewModel.Apps, row => row.Pid == 100);
        Assert.DoesNotContain(viewModel.Background, row => row.Pid == 300);
    }

    /// <summary>One protected process must not stop the rest — Task Manager's own behaviour, and the
    /// only sane one when the user asked for five.</summary>
    [Fact]
    public async Task ConfirmEndTask_OneRefusal_StillEndsTheOthersAndCountsIt() {
        var (viewModel, terminator) = Endable();
        await viewModel.LoadAsync();
        terminator.Outcomes[300] = ProcessEndOutcome.Denied;
        viewModel.SetGroupSelected(ProcessCategory.App, selected: true);
        viewModel.SelectRow(Row(viewModel, 300), extend: true, range: false);

        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.Equal([100, 200, 300], terminator.Ended.OrderBy(pid => pid));
        // The refusal keeps its row and its place in the selection; the other two are gone.
        Assert.Equal([300], viewModel.SelectedPids);
        // One failure is named rather than counted — the name is what tells the user which it was.
        Assert.Equal("Couldn't end helper.exe — it needs administrator rights", viewModel.ActionMessage);
    }

    [Fact]
    public async Task ConfirmEndTask_SeveralRefusals_CountsThemAgainstWhatWasAsked() {
        var (viewModel, terminator) = Endable();
        await viewModel.LoadAsync();
        terminator.Outcomes[100] = ProcessEndOutcome.Denied;
        terminator.Outcomes[300] = ProcessEndOutcome.Denied;
        // browser.exe sorts first, so this range is every row on screen.
        viewModel.SelectRange(200, 400);

        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't end 2 of 4 processes — 2 need administrator rights", viewModel.ActionMessage);
        Assert.Equal([100, 300], viewModel.SelectedPids.OrderBy(pid => pid));
    }

    [Fact]
    public async Task ConfirmEndTask_SingleRefusal_NamesTheProcess() {
        var (viewModel, terminator) = Endable();
        await viewModel.LoadAsync();
        terminator.Outcomes[100] = ProcessEndOutcome.Denied;
        viewModel.SelectRow(Row(viewModel, 100));

        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't end editor.exe — it needs administrator rights", viewModel.ActionMessage);
    }

    /// <summary>The selection outlives the filter, so what it holds is what gets ended — including a
    /// process currently filtered out of sight.</summary>
    [Fact]
    public async Task ConfirmEndTask_EndsASelectedProcessTheFilterIsHiding() {
        var (viewModel, terminator) = Endable();
        await viewModel.LoadAsync();
        viewModel.SelectRow(Row(viewModel, 100));
        viewModel.FilterText = "helper";

        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.Equal([100], terminator.Ended);
    }

    [Fact]
    public async Task RequestEndTask_ReadsAsOneProcessOrMany() {
        var (viewModel, _) = Endable();
        await viewModel.LoadAsync();

        viewModel.SelectRow(Row(viewModel, 100));
        viewModel.RequestEndTaskCommand.Execute(null);
        Assert.True(viewModel.ConfirmVisible);
        Assert.Contains("editor.exe", viewModel.ConfirmText);

        viewModel.CancelEndTaskCommand.Execute(null);
        viewModel.SetGroupSelected(ProcessCategory.App, selected: true);
        viewModel.RequestEndTaskCommand.Execute(null);
        Assert.Contains("these 2 processes", viewModel.ConfirmText);
    }

    [Fact]
    public async Task RequestEndTask_WithNothingSelected_ShowsNothing() {
        var (viewModel, _) = Endable();
        await viewModel.LoadAsync();

        viewModel.RequestEndTaskCommand.Execute(null);

        Assert.False(viewModel.ConfirmVisible);
    }

    // ----- First load -----

    /// <summary>Before the first enumeration comes back the page must not claim anything: an empty list
    /// and a confident "0" read as a machine running no processes.</summary>
    [Fact]
    public async Task HasLoaded_IsFalseUntilTheFirstEnumerationLands() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var provider = new ControllableSnapshotProvider([Proc(100, 0, "editor.exe", ProcessCategory.App)]) {
            Gate = new TaskCompletionSource(),
        };
        var viewModel = new ProcessesViewModel(metrics, provider, new FakeProcessInterop());

        var load = viewModel.LoadAsync();
        Assert.False(viewModel.HasLoaded);
        Assert.Equal(Placeholders.NoReading, viewModel.TotalProcessesText);
        Assert.Equal(Placeholders.NoReading, viewModel.ThreadsText);

        provider.Gate.SetResult();
        await load;

        Assert.True(viewModel.HasLoaded);
        Assert.Equal("1", viewModel.TotalProcessesText);
    }

    /// <summary>A page that failed still has an answer — an empty list, honestly labelled. Leaving
    /// HasLoaded false would sit under the placeholder for the rest of the session.</summary>
    [Fact]
    public async Task HasLoaded_IsTrueAfterAFailedEnumerationToo() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var provider = new ControllableSnapshotProvider([]) {
            Fail = new InvalidOperationException("the enumeration broke"),
        };
        var viewModel = new ProcessesViewModel(metrics, provider, new FakeProcessInterop());

        await viewModel.LoadAsync();

        Assert.True(viewModel.HasLoaded);
        Assert.Empty(viewModel.Apps);
        Assert.Equal(Placeholders.NoReading, viewModel.TotalProcessesText);
    }

    // ----- Remembered preferences -----

    /// <summary>Off by default: folding a section or re-sorting must not quietly become a preference.</summary>
    [Fact]
    public async Task RememberToggles_StartOff() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();

        Assert.False(viewModel.RememberCollapsedGroups);
        Assert.False(viewModel.RememberSort);
    }

    [Fact]
    public async Task ToggleGroup_ReportsOnlyWhileCollapsedSectionsAreRemembered() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();
        var reported = 0;
        viewModel.PreferencesChanged += () => reported++;

        viewModel.ToggleGroup(ProcessCategory.App);
        Assert.Equal(0, reported);

        // Switching the toggle on is itself worth saving.
        viewModel.RememberCollapsedGroups = true;
        Assert.Equal(1, reported);

        viewModel.ToggleGroup(ProcessCategory.Background);
        Assert.Equal(2, reported);
    }

    [Fact]
    public async Task Sorting_ReportsOnlyWhileTheSortIsRemembered() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();
        var reported = 0;
        viewModel.PreferencesChanged += () => reported++;

        viewModel.CpuSort.SortCommand.Execute(null);
        Assert.Equal(0, reported);

        viewModel.RememberSort = true;
        Assert.Equal(1, reported);

        viewModel.MemorySort.SortCommand.Execute(null);
        Assert.Equal(2, reported);

        // Alt+arrow sets the direction on the column already sorted, and counts the same way.
        viewModel.SetSortDirection(ascending: true);
        Assert.Equal(3, reported);
    }

    /// <summary>Seeding a saved sort must not write straight back to the settings file.</summary>
    [Fact]
    public async Task SortKeyAndDirection_AssignedFromSettings_AreQuiet() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();
        viewModel.RememberSort = true;
        var reported = 0;
        viewModel.PreferencesChanged += () => reported++;

        viewModel.SortKey = ProcessSortKey.Memory;
        viewModel.SortAscending = false;

        Assert.Equal(0, reported);
        Assert.Equal(ProcessSortKey.Memory, viewModel.SortKey);
        Assert.False(viewModel.SortAscending);
        Assert.True(viewModel.MemorySort.IsActive);
    }

    [Fact]
    public async Task CollapsedGroups_AssignedFromSettings_AreApplied() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();

        viewModel.CollapsedGroups = new[] { ProcessCategory.Background, ProcessCategory.Windows };

        Assert.False(viewModel.AppsCollapsed);
        Assert.True(viewModel.BackgroundCollapsed);
        Assert.True(viewModel.WindowsCollapsed);
    }

    // ----- Collapsible groups -----

    [Fact]
    public async Task ToggleGroup_FoldsAndUnfoldsJustThatGroup() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();

        viewModel.ToggleGroup(ProcessCategory.App);

        Assert.True(viewModel.AppsCollapsed);
        Assert.False(viewModel.BackgroundCollapsed);
        Assert.False(viewModel.WindowsCollapsed);
        Assert.Equal("▸", viewModel.AppsChevron);
        Assert.Equal("▾", viewModel.BackgroundChevron);

        viewModel.ToggleGroup(ProcessCategory.App);
        Assert.False(viewModel.AppsCollapsed);
        Assert.Equal("▾", viewModel.AppsChevron);
    }

    /// <summary>Folding hides the list, it does not empty it: the heading's count, the filter and the
    /// selection all keep meaning the same thing while a group is shut.</summary>
    [Fact]
    public async Task ToggleGroup_LeavesTheRowsAndTheirSelectionAlone() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();
        viewModel.SetGroupSelected(ProcessCategory.App, selected: true);

        viewModel.ToggleGroup(ProcessCategory.App);

        Assert.Equal(2, viewModel.Apps.Count);
        Assert.Equal([100, 200], viewModel.SelectedPids.OrderBy(pid => pid));
        Assert.True(viewModel.AppsAllSelected);
        Assert.Contains("Apps · 2", viewModel.AppsHeader);
    }

    [Fact]
    public async Task ToggleGroup_SurvivesAPoll() {
        var (viewModel, _) = Selectable();
        await viewModel.LoadAsync();
        viewModel.ToggleGroup(ProcessCategory.Windows);

        await viewModel.LoadAsync();

        Assert.True(viewModel.WindowsCollapsed);
    }

    // ----- End task: confirming the kill took -----

    /// <summary>The same fixture as <see cref="Endable"/>, over a batch whose waits cost nothing, so a
    /// process that never exits is reported without spending the real budget.</summary>
    private static (ProcessesViewModel ViewModel, FakeProcessTerminator Terminator,
                    ControllableSnapshotProvider Provider) Verifying() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var provider = new ControllableSnapshotProvider([
            Proc(100, 0, "editor.exe", ProcessCategory.App),
            Proc(300, 0, "helper.exe", ProcessCategory.Background),
        ]);
        var terminator = new FakeProcessTerminator();
        var batch = new ProcessEndBatch(
            terminator, TimeSpan.FromMilliseconds(200), (_, _) => Task.CompletedTask);

        return (new ProcessesViewModel(metrics, provider, new FakeProcessInterop(), terminator, batch),
                terminator, provider);
    }

    /// <summary>The bug behind "it looked like it worked": a process that accepts the kill and then does
    /// not go used to lose its row anyway, and the next poll put it back.</summary>
    [Fact]
    public async Task ConfirmEndTask_ProcessNeverExits_KeepsItsRowAndItsSelection() {
        var (viewModel, terminator, _) = Verifying();
        await viewModel.LoadAsync();
        terminator.Lingering.Add(100);
        viewModel.SelectRow(Row(viewModel, 100));
        viewModel.SelectRow(Row(viewModel, 300), extend: true, range: false);

        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.Apps, row => row.Pid == 100);
        Assert.DoesNotContain(viewModel.Background, row => row.Pid == 300);
        Assert.Equal([100], viewModel.SelectedPids);
        Assert.Equal("Couldn't end editor.exe — it didn't respond", viewModel.ActionMessage);
    }

    /// <summary>An already-exited process is a removed row and nothing said about it — it used to read
    /// as a failure the user could do nothing about.</summary>
    [Fact]
    public async Task ConfirmEndTask_ProcessAlreadyGone_DropsTheRowWithoutComplaining() {
        var (viewModel, terminator, _) = Verifying();
        await viewModel.LoadAsync();
        terminator.Outcomes[100] = ProcessEndOutcome.AlreadyGone;
        viewModel.SelectRow(Row(viewModel, 100));

        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.DoesNotContain(viewModel.Apps, row => row.Pid == 100);
        Assert.Equal("", viewModel.ActionMessage);
    }

    [Fact]
    public async Task ConfirmEndTask_MixedFailures_NamesEachReason() {
        var (viewModel, terminator, _) = Verifying();
        await viewModel.LoadAsync();
        terminator.Outcomes[100] = ProcessEndOutcome.Denied;
        terminator.Lingering.Add(300);
        viewModel.SelectRow(Row(viewModel, 100));
        viewModel.SelectRow(Row(viewModel, 300), extend: true, range: false);

        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't end 2 of 2 processes — 1 needs administrator rights, 1 didn't respond",
                     viewModel.ActionMessage);
    }

    /// <summary>A poll can retire a PID while the overlay is up, so the scope is re-derived on confirm
    /// rather than carried over from the prompt.</summary>
    [Fact]
    public async Task ConfirmEndTask_AfterAPollDropsAProcess_EndsOnlyWhatIsLeft() {
        var (viewModel, terminator, provider) = Verifying();
        await viewModel.LoadAsync();
        viewModel.SelectRow(Row(viewModel, 100));
        viewModel.SelectRow(Row(viewModel, 300), extend: true, range: false);
        viewModel.RequestEndTaskCommand.Execute(null);

        provider.Processes = [Proc(300, 0, "helper.exe", ProcessCategory.Background)];
        await viewModel.LoadAsync();
        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.Equal([300], terminator.Ended);
    }

    // ----- End task: collapsed groups -----

    /// <summary>An Edge-shaped group (700 with helpers 701 and 702) beside two flat processes, so a
    /// collapsed row really does stand for more than itself.</summary>
    private static (ProcessesViewModel ViewModel, FakeProcessTerminator Terminator) EndableGroup() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var provider = new ControllableSnapshotProvider([
            Proc(700, 0, "browser.exe", ProcessCategory.App),
            Proc(701, 700, "browser.exe", ProcessCategory.App),
            Proc(702, 700, "browser.exe", ProcessCategory.App),
            Proc(800, 0, "editor.exe", ProcessCategory.App),
            Proc(900, 0, "helper.exe", ProcessCategory.Background),
        ]);
        var terminator = new FakeProcessTerminator();

        return (new ProcessesViewModel(metrics, provider, new FakeProcessInterop(), terminator), terminator);
    }

    /// <summary>The bug: the row shows the whole group's aggregate, so ending it must end the whole
    /// group. It used to end the root alone and leave the helpers running.</summary>
    [Fact]
    public async Task ConfirmEndTask_CollapsedGroup_EndsEveryProcessUnderIt() {
        var (viewModel, terminator) = EndableGroup();
        await viewModel.LoadAsync();
        viewModel.SelectRow(Row(viewModel, 700));

        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.Equal([700, 701, 702], terminator.Ended.OrderBy(pid => pid));
    }

    /// <summary>An expanded group's children are rows of their own, so the parent stands only for
    /// itself.</summary>
    [Fact]
    public async Task ConfirmEndTask_ExpandedGroup_EndsOnlyTheRowsPicked() {
        var (viewModel, terminator) = EndableGroup();
        await viewModel.LoadAsync();
        viewModel.ToggleExpand(Row(viewModel, 700));
        viewModel.SelectRow(Row(viewModel, 700));

        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.Equal([700], terminator.Ended);
    }

    [Fact]
    public async Task ConfirmEndTask_CollapsedGroup_DropsTheChildRowsToo() {
        var (viewModel, terminator) = EndableGroup();
        await viewModel.LoadAsync();
        viewModel.ToggleExpand(Row(viewModel, 700));
        viewModel.SelectRow(Row(viewModel, 700));
        viewModel.ToggleExpand(Row(viewModel, 700));

        await viewModel.ConfirmEndTaskCommand.ExecuteAsync(null);

        Assert.Equal([700, 701, 702], terminator.Ended.OrderBy(pid => pid));
        Assert.DoesNotContain(viewModel.Apps, row => row.Pid is 700 or 701 or 702);
        Assert.Contains(viewModel.Apps, row => row.Pid == 800);
    }

    /// <summary>One row, many processes — the prompt has to say so, or it promises to end one thing and
    /// ends three.</summary>
    [Fact]
    public async Task RequestEndTask_CollapsedGroup_CountsWhatItWillActuallyEnd() {
        var (viewModel, _) = EndableGroup();
        await viewModel.LoadAsync();
        viewModel.SelectRow(Row(viewModel, 700));

        viewModel.RequestEndTaskCommand.Execute(null);

        Assert.Contains("browser.exe", viewModel.ConfirmText);
        Assert.Contains("2 processes grouped under it", viewModel.ConfirmText);
    }

    [Fact]
    public async Task RequestEndTask_GroupAndAFlatRow_SeparatesTheRowsFromWhatTheyGroup() {
        var (viewModel, _) = EndableGroup();
        await viewModel.LoadAsync();
        viewModel.SelectRow(Row(viewModel, 700));
        viewModel.SelectRow(Row(viewModel, 800), extend: true, range: false);

        viewModel.RequestEndTaskCommand.Execute(null);

        Assert.Contains("2 selected items and the 2 processes they group", viewModel.ConfirmText);
    }

    /// <summary>Two childless rows still read as a plain count, unchanged by the tree walk.</summary>
    [Fact]
    public async Task RequestEndTask_FlatRowsOnly_ReadsAsAPlainCount() {
        var (viewModel, _) = EndableGroup();
        await viewModel.LoadAsync();
        viewModel.SelectRow(Row(viewModel, 800));
        viewModel.SelectRow(Row(viewModel, 900), extend: true, range: false);

        viewModel.RequestEndTaskCommand.Execute(null);

        Assert.Equal("End these 2 processes? Any unsaved work in them will be lost.", viewModel.ConfirmText);
    }

    /// <summary>Records what End task asked to kill, and answers with whatever outcome it is told to.
    /// A PID left out of <see cref="Outcomes"/> ends and exits cleanly.</summary>
    private sealed class FakeProcessTerminator : IProcessTerminator {
        public List<int> Ended { get; } = [];
        public Dictionary<int, ProcessEndOutcome> Outcomes { get; } = [];

        /// <summary>PIDs that accept the kill but never actually exit, for the bounded wait.</summary>
        public HashSet<int> Lingering { get; } = [];

        public ProcessEndOutcome Request(int pid) {
            Ended.Add(pid);
            return Outcomes.TryGetValue(pid, out var outcome) ? outcome : ProcessEndOutcome.Ended;
        }

        public bool HasExited(int pid) => !Lingering.Contains(pid);
    }

    private sealed class FakeSnapshotProvider(IReadOnlyList<ProcessInfo> processes) : IProcessSnapshotProvider {
        public Task<IReadOnlyList<ProcessInfo>> GetAsync(CancellationToken token = default) => Task.FromResult(processes);
    }

    /// <summary>A provider the test can park and then fail on demand, so a load can be caught in flight
    /// and made to come back badly after the page has gone off screen.</summary>
    private sealed class ControllableSnapshotProvider(IReadOnlyList<ProcessInfo> processes)
        : IProcessSnapshotProvider {
        public TaskCompletionSource? Gate { get; set; }
        public Exception? Fail { get; set; }

        /// <summary>What the next poll returns, so a test can retire a process mid-run.</summary>
        public IReadOnlyList<ProcessInfo> Processes { get; set; } = processes;

        public async Task<IReadOnlyList<ProcessInfo>> GetAsync(CancellationToken token = default) {
            if (Gate is { } gate)
                await gate.Task;
            if (Fail is { } failure)
                throw failure;
            return Processes;
        }
    }

    private sealed class FakeProcessInterop : IProcessInterop {
        public List<(IntPtr Owner, int Pid)> Calls { get; } = [];
        public bool TryGetIoBytes(Process process, out ulong totalBytes) {
            totalBytes = 0;
            return false;
        }
        public void ShowProperties(IntPtr owner, int pid) => Calls.Add((owner, pid));
    }
}
