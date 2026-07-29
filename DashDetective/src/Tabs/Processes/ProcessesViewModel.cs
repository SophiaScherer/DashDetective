using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// The Processes tab: a live, Task-Manager-style process view. Like the Dashboard and Network tabs it
/// is always-on — constructed once by the shell and left running — so it implements
/// <see cref="IRefreshablePage"/> (toolbar Refresh), <see cref="ILiveSamplingPage"/> (toolbar Live
/// pill) and <see cref="IDisposable"/>. It fills the viewport and scrolls its own table, so it is also
/// an <see cref="ISelfScrollingPage"/>.
///
/// Each poll takes an off-UI-thread snapshot (<see cref="ProcessSnapshotProvider"/>), splits it into
/// Apps and Background, orders each group, and reconciles it into the matching observable collection by
/// PID — the keyed diff from the Network connections table, so rows are reused and the list doesn't
/// flicker. Sorting, filtering and expand/collapse all re-project the snapshot already in hand rather
/// than re-enumerating, so they feel instant between polls.
/// </summary>
public partial class ProcessesViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, ISelfScrollingPage, IShortcutTarget, IDisposable {
    /// <summary>Poll cadence. Enumerating every process (with per-process window/responding probes) is
    /// heavier than a single counter, so it polls slower than the Dashboard's 1 Hz samplers — close to
    /// Task Manager's own refresh.</summary>
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(2);

    private readonly DispatcherTimer _timer;
    private bool _inFlight;

    // System-wide CPU% / Memory% for the summary strip — the same readings the Dashboard shows, from the
    // shared SystemMetricsService (so there's one sampler across all tabs).
    private readonly SystemMetricsService _service;
    private readonly IDisposable[] _subscriptions;

    // Sort state: which column + direction. Sorting applies within each group; Apps stay above
    // Background. Defaults to Name ascending (matching the initial list order).
    private ProcessSortKey _sortKey = ProcessSortKey.Name;
    private bool _ascending = true;
    private readonly ProcessSortColumn[] _sortColumns;

    /// <summary>The last snapshot, kept so a header click can re-sort immediately without waiting for
    /// the next poll.</summary>
    private IReadOnlyList<ProcessInfo> _lastSnapshot = Array.Empty<ProcessInfo>();

    /// <summary>The last built process tree (top-level entries with their collapsed children). Kept so
    /// the expand/collapse chevrons can re-flatten the visible rows without rebuilding the tree.</summary>
    private IReadOnlyList<ProcessNode> _lastRoots = Array.Empty<ProcessNode>();

    /// <summary>PIDs whose children are currently revealed. The authoritative expand state (rows are
    /// transient — the keyed diff recreates them), so it survives polls and re-sorts.</summary>
    private readonly HashSet<int> _expandedPids = new();

    /// <summary>Foreground apps (own a visible top-level window), updated in place by the keyed diff.
    /// Holds one row per top-level group (a multi-process app collapses to a single entry).</summary>
    public ObservableCollection<ProcessRow> Apps { get; } = new();

    /// <summary>Background processes (user-session helpers/trays/updaters with no window), updated in
    /// place.</summary>
    public ObservableCollection<ProcessRow> Background { get; } = new();

    /// <summary>Windows processes (system/service processes outside the interactive session), updated in
    /// place — Task Manager's third group.</summary>
    public ObservableCollection<ProcessRow> WindowsProcesses { get; } = new();

    // Clickable column headers.
    public ProcessSortColumn NameSort { get; }
    public ProcessSortColumn PidSort { get; }
    public ProcessSortColumn StatusSort { get; }
    public ProcessSortColumn CpuSort { get; }
    public ProcessSortColumn MemorySort { get; }
    public ProcessSortColumn DiskSort { get; }
    public ProcessSortColumn GpuSort { get; }

    /// <summary>Group header caption for the Apps section (e.g. "Apps · 6").</summary>
    [ObservableProperty] private string _appsHeader = "Apps";

    /// <summary>Group header caption for the Background section (e.g. "Background processes · 127").</summary>
    [ObservableProperty] private string _backgroundHeader = "Background processes";

    /// <summary>Group header caption for the Windows-processes section (e.g. "Windows processes · 150").</summary>
    [ObservableProperty] private string _windowsHeader = "Windows processes";

    // ----- Summary strip -----

    /// <summary>Total live process count, for the Processes summary card.</summary>
    [ObservableProperty] private string _totalProcessesText = "0";

    /// <summary>Per-group breakdown under the total (e.g. "10 apps · 310 background").</summary>
    [ObservableProperty] private string _processBreakdownText = "";

    /// <summary>System-wide CPU utilisation, whole percent (e.g. "12%").</summary>
    [ObservableProperty] private string _cpuUsageText = "0%";

    /// <summary>System-wide physical-memory usage, whole percent (e.g. "49%").</summary>
    [ObservableProperty] private string _memoryUsageText = "0%";

    /// <summary>Total thread count across all processes (e.g. "2,418").</summary>
    [ObservableProperty] private string _threadsText = "0";

    // ----- Selection + actions -----

    /// <summary>The currently selected row (across both groups), or null. Drives End task / Properties
    /// enablement and the row highlight.</summary>
    [ObservableProperty] private ProcessRow? _selectedRow;

    /// <summary>Whether a row is selected — enables the End task and Properties buttons.</summary>
    public bool HasSelection => SelectedRow is not null;

    /// <summary>Whether the End-task confirmation overlay is showing.</summary>
    [ObservableProperty] private bool _confirmVisible;

    /// <summary>The confirmation prompt for the process being ended.</summary>
    [ObservableProperty] private string _confirmText = "";

    /// <summary>Transient feedback after an action (e.g. a soft-failed End task). Cleared on the next
    /// selection or successful action.</summary>
    [ObservableProperty] private string _actionMessage = "";

    partial void OnSelectedRowChanged(ProcessRow? value) => OnPropertyChanged(nameof(HasSelection));

    // ----- Filtering -----

    /// <summary>Narrows the three groups to processes matching a name substring or PID prefix. Applied
    /// to the rows already in hand, so typing re-filters instantly without waiting for the next poll.</summary>
    [ObservableProperty] private string _filterText = "";

    partial void OnFilterTextChanged(string value) => RebuildVisibleRows();

    /// <summary>Clears the filter (the box's × button, and Esc while the box has content).</summary>
    [RelayCommand]
    private void ClearFilter() => FilterText = "";

    /// <summary>Raised when the focus-filter shortcut fires, so the view can put the caret in the box.
    /// UI-only; carries no state — the same view/view-model seam the File Explorer uses for scrolling.</summary>
    public event Action? FilterFocusRequested;

    public ProcessesViewModel(SystemMetricsService service) {
        _service = service;
        NameSort = new ProcessSortColumn(ProcessSortKey.Name, OnSort);
        PidSort = new ProcessSortColumn(ProcessSortKey.Pid, OnSort);
        StatusSort = new ProcessSortColumn(ProcessSortKey.Status, OnSort);
        CpuSort = new ProcessSortColumn(ProcessSortKey.Cpu, OnSort);
        MemorySort = new ProcessSortColumn(ProcessSortKey.Memory, OnSort);
        DiskSort = new ProcessSortColumn(ProcessSortKey.Disk, OnSort);
        GpuSort = new ProcessSortColumn(ProcessSortKey.Gpu, OnSort);
        _sortColumns = new[] {
            NameSort, PidSort, StatusSort, CpuSort, MemorySort, DiskSort, GpuSort,
        };
        UpdateSortIndicators();

        // The summary CPU%/Memory% come from the shared service (subscribe replays the latest value at
        // once). The process list loads and polls on its own timer below.
        _subscriptions = new[] {
            _service.SubscribeCpu(OnCpuTotal, OnCpuTotalFailed),
            _service.SubscribeMemory(OnMemoryTotal, OnMemoryTotalFailed),
        };
        _ = LoadAsync();

        _timer = new DispatcherTimer { Interval = SampleInterval };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e) => _ = LoadAsync();

    /// <summary>Summary CPU% callback.</summary>
    private void OnCpuTotal(double cpu) => CpuUsageText = FormatPercent(cpu);

    /// <summary>Summary Memory% callback.</summary>
    private void OnMemoryTotal(MemorySample memory) => MemoryUsageText = FormatPercent(memory.LoadPercent);

    /// <summary>On a CPU sampler failure, keep the current summary at 0% (matches the old soft-fail).</summary>
    private void OnCpuTotalFailed() => CpuUsageText = FormatPercent(0);

    /// <summary>On a memory sampler failure, keep the current summary at 0% (matches the old soft-fail).</summary>
    private void OnMemoryTotalFailed() => MemoryUsageText = FormatPercent(0);

    private static string FormatPercent(double percent) {
        if (percent < 0)
            percent = 0;
        return Math.Round(percent).ToString(CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Reads the snapshot off the UI thread and applies it. Guarded against overlap (a slow
    /// enumeration must not pile up ticks) and never throws.</summary>
    private async Task LoadAsync() {
        if (_inFlight)
            return;
        _inFlight = true;
        try {
            var processes = await ProcessSnapshotProvider.GetAsync();
            // Awaited on the UI thread, so the continuation resumes there — safe to touch collections.
            _lastSnapshot = processes;
            ApplySnapshot(processes);
        } catch {
            _lastSnapshot = Array.Empty<ProcessInfo>();
            _lastRoots = Array.Empty<ProcessNode>();
            Apps.Clear();
            Background.Clear();
            WindowsProcesses.Clear();
            AppsHeader = "Apps";
            BackgroundHeader = "Background processes";
            WindowsHeader = "Windows processes";
            TotalProcessesText = "0";
            ProcessBreakdownText = "";
            ThreadsText = "0";
        } finally {
            _inFlight = false;
        }
    }

    /// <summary>A single visible row in a group: a tree node projected for display — its aggregate
    /// metrics plus where it sits in the tree (depth, whether it has children, whether it's expanded).</summary>
    private readonly record struct RowModel(ProcessInfo Info, int Depth, bool HasChildren, bool IsExpanded);

    /// <summary>Builds the process tree (collapsing multi-process apps into one entry, Task-Manager
    /// style) and flattens it into the visible rows. Kept for the next poll / a header re-sort.</summary>
    private void ApplySnapshot(IReadOnlyList<ProcessInfo> processes) {
        _lastRoots = ProcessTreeBuilder.Build(processes);
        PruneExpanded(_lastRoots);
        RebuildVisibleRows();
    }

    /// <summary>Drops expand state for PIDs that no longer name a live parent (exited or lost their
    /// children), so the set doesn't accumulate stale entries across polls.</summary>
    private void PruneExpanded(IReadOnlyList<ProcessNode> roots) {
        if (_expandedPids.Count == 0)
            return;
        var expandable = new HashSet<int>();
        CollectExpandable(roots, expandable);
        _expandedPids.IntersectWith(expandable);
    }

    private static void CollectExpandable(IReadOnlyList<ProcessNode> nodes, HashSet<int> into) {
        foreach (var node in nodes) {
            if (node.HasChildren) {
                into.Add(node.Info.Pid);
                CollectExpandable(node.Children, into);
            }
        }
    }

    /// <summary>Splits the tree's top-level entries into the three groups, orders each by the active
    /// sort key, flattens expanded subtrees into visible rows, reconciles them into place and updates
    /// the captions. Each row shows its node's aggregate metrics (own + descendants). Called on every
    /// poll, header re-sort and expand/collapse.</summary>
    private void RebuildVisibleRows() {
        var appRoots = new List<ProcessNode>();
        var backgroundRoots = new List<ProcessNode>();
        var windowsRoots = new List<ProcessNode>();
        foreach (var node in _lastRoots) {
            switch (node.Aggregate.Category) {
                case ProcessCategory.App: appRoots.Add(node); break;
                case ProcessCategory.Windows: windowsRoots.Add(node); break;
                default: backgroundRoots.Add(node); break;
            }
        }

        // The summary strip describes the machine, so it counts every entry; the lists and their group
        // headers describe what's on screen, so they count what survives the filter.
        var visibleApps = ApplyFilter(appRoots);
        var visibleBackground = ApplyFilter(backgroundRoots);
        var visibleWindows = ApplyFilter(windowsRoots);

        var appRows = Flatten(visibleApps);
        var backgroundRows = Flatten(visibleBackground);
        var windowsRows = Flatten(visibleWindows);

        Reconcile(Apps, appRows);
        Reconcile(Background, backgroundRows);
        Reconcile(WindowsProcesses, windowsRows);

        // If the selected process has exited, the diff removed its row — drop the dangling selection.
        if (SelectedRow is not null && !Apps.Contains(SelectedRow) &&
            !Background.Contains(SelectedRow) && !WindowsProcesses.Contains(SelectedRow)) {
            SelectedRow.IsSelected = false;
            SelectedRow = null;
        }

        // Group headers and the summary count top-level entries (roots), not the expanded rows, so the
        // numbers stay put when a group is expanded.
        AppsHeader = $"Apps · {visibleApps.Count.ToString(CultureInfo.InvariantCulture)}";
        BackgroundHeader = $"Background processes · {visibleBackground.Count.ToString(CultureInfo.InvariantCulture)}";
        WindowsHeader = $"Windows processes · {visibleWindows.Count.ToString(CultureInfo.InvariantCulture)}";

        // Total threads span every process, not just the top-level entries.
        var totalThreads = 0;
        foreach (var info in _lastSnapshot)
            totalThreads += info.ThreadCount;

        var entries = appRoots.Count + backgroundRoots.Count + windowsRoots.Count;
        TotalProcessesText = entries.ToString(CultureInfo.InvariantCulture);
        ProcessBreakdownText = $"{appRoots.Count.ToString(CultureInfo.InvariantCulture)} apps · " +
                               $"{backgroundRoots.Count.ToString(CultureInfo.InvariantCulture)} background · " +
                               $"{windowsRoots.Count.ToString(CultureInfo.InvariantCulture)} Windows";
        ThreadsText = totalThreads.ToString("N0", CultureInfo.InvariantCulture);
    }

    /// <summary>Keeps the roots the filter matches. A parent survives when any of its children match, so
    /// filtering never hides an entry the match is nested inside; expanding it then reveals the hit.</summary>
    private List<ProcessNode> ApplyFilter(List<ProcessNode> roots) {
        if (string.IsNullOrWhiteSpace(FilterText))
            return roots;

        var kept = new List<ProcessNode>(roots.Count);
        foreach (var root in roots)
            if (MatchesFilter(root))
                kept.Add(root);

        return kept;
    }

    private bool MatchesFilter(ProcessNode node) {
        if (ProcessFilter.Matches(node.Info.Name, node.Info.Pid, FilterText))
            return true;

        foreach (var child in node.Children)
            if (MatchesFilter(child))
                return true;

        return false;
    }

    /// <summary>Orders the roots and flattens each expanded subtree depth-first into the visible-row
    /// list: a parent is immediately followed by its children (when expanded), each ordered by the same
    /// active sort key.</summary>
    private List<RowModel> Flatten(List<ProcessNode> roots) {
        roots.Sort(CompareNodes);
        var rows = new List<RowModel>(roots.Count);
        foreach (var root in roots)
            FlattenInto(root, 0, rows);
        return rows;
    }

    private void FlattenInto(ProcessNode node, int depth, List<RowModel> rows) {
        var expanded = node.HasChildren && _expandedPids.Contains(node.Info.Pid);
        rows.Add(new RowModel(node.Aggregate, depth, node.HasChildren, expanded));
        if (!expanded)
            return;

        var children = new List<ProcessNode>(node.Children);
        children.Sort(CompareNodes);
        foreach (var child in children)
            FlattenInto(child, depth + 1, rows);
    }

    private int CompareNodes(ProcessNode a, ProcessNode b) => Compare(a.Aggregate, b.Aggregate);

    /// <summary>Toggles a parent entry's expanded state and re-flattens the visible rows (from the tree
    /// already built this poll — no re-enumeration). Driven from the view code-behind on a chevron tap,
    /// like row selection.</summary>
    public void ToggleExpand(ProcessRow row) {
        if (!row.HasChildren)
            return;
        if (!_expandedPids.Remove(row.Pid))
            _expandedPids.Add(row.Pid);
        RebuildVisibleRows();
    }

    /// <summary>Header click: flip direction if it's the same column, else switch to the new column at
    /// its Explorer-style default direction. Re-sorts the current data immediately (so the click feels
    /// instant) rather than waiting for the next poll.</summary>
    private void OnSort(ProcessSortKey key) {
        if (_sortKey == key) {
            _ascending = !_ascending;
        } else {
            _sortKey = key;
            _ascending = DefaultAscending(key);
        }
        UpdateSortIndicators();
        RebuildVisibleRows();
    }

    /// <summary>Sets the direction on whichever column is already sorted, leaving the column itself
    /// alone (Alt+↑ / Alt+↓). Asking for the direction already in effect is a no-op, but still counts as
    /// handled so the key isn't passed on to scroll the list.</summary>
    public bool SetSortDirection(bool ascending) {
        if (_ascending != ascending) {
            _ascending = ascending;
            UpdateSortIndicators();
            RebuildVisibleRows();
        }

        return true;
    }

    /// <summary>Explorer-style defaults: text columns ascending, magnitude columns busiest-first.</summary>
    private static bool DefaultAscending(ProcessSortKey key) => key switch {
        ProcessSortKey.Name => true,
        ProcessSortKey.Pid => true,
        ProcessSortKey.Status => true,
        _ => false, // CPU / Memory / Disk / GPU
    };

    /// <summary>Tints the active column and shows its ↑/↓ arrow; clears the rest.</summary>
    private void UpdateSortIndicators() {
        foreach (var column in _sortColumns) {
            column.IsActive = column.Key == _sortKey;
            column.Arrow = column.IsActive ? (_ascending ? "↑" : "↓") : "";
        }
    }

    /// <summary>Orders two processes by the active sort key + direction, always breaking ties by name
    /// then PID so the live list stays deterministic (no jitter on equal keys).</summary>
    private int Compare(ProcessInfo a, ProcessInfo b) {
        var cmp = _sortKey switch {
            ProcessSortKey.Name => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
            ProcessSortKey.Pid => a.Pid.CompareTo(b.Pid),
            ProcessSortKey.Status => string.Compare(a.Status, b.Status, StringComparison.OrdinalIgnoreCase),
            ProcessSortKey.Cpu => a.CpuPercent.CompareTo(b.CpuPercent),
            ProcessSortKey.Memory => a.MemoryBytes.CompareTo(b.MemoryBytes),
            ProcessSortKey.Disk => a.DiskBytesPerSec.CompareTo(b.DiskBytesPerSec),
            ProcessSortKey.Gpu => a.GpuPercent.CompareTo(b.GpuPercent),
            _ => 0, // Unreachable — every key is handled above; the arm satisfies exhaustiveness.
        };
        if (!_ascending)
            cmp = -cmp;
        if (cmp != 0)
            return cmp;

        var byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        return byName != 0 ? byName : a.Pid.CompareTo(b.Pid);
    }

    /// <summary>Diffs an already-ordered snapshot into <paramref name="target"/> by PID, via the shared
    /// <see cref="CollectionReconciler"/> (drop/update/move/insert in place, no flicker).</summary>
    private static void Reconcile(ObservableCollection<ProcessRow> target, IReadOnlyList<RowModel> incoming) =>
        CollectionReconciler.Reconcile(target, incoming,
            static row => row.Pid, static model => model.Info.Pid,
            static (row, model) => row.Update(model.Info, model.Depth, model.HasChildren, model.IsExpanded),
            static model => new ProcessRow(model.Info, model.Depth, model.HasChildren, model.IsExpanded));

    /// <summary>Selects a row (single selection across both groups), clearing the previous one. Driven
    /// from the view code-behind on tap, like File Explorer's row selection.</summary>
    public void SelectRow(ProcessRow row) {
        if (ReferenceEquals(SelectedRow, row))
            return;
        if (SelectedRow is not null)
            SelectedRow.IsSelected = false;
        row.IsSelected = true;
        SelectedRow = row;
        ActionMessage = "";
    }

    /// <summary>End task button: shows the confirmation overlay for the selected process (killing a
    /// process is destructive, so it isn't done on a single click).</summary>
    [RelayCommand]
    private void RequestEndTask() {
        if (SelectedRow is null)
            return;
        ConfirmText = $"End “{SelectedRow.Name}”? Any unsaved work in this process will be lost.";
        ConfirmVisible = true;
    }

    /// <summary>Dismisses the confirmation overlay without ending anything.</summary>
    [RelayCommand]
    private void CancelEndTask() => ConfirmVisible = false;

    /// <summary>Confirms the End task: terminates the process and removes its row immediately (the next
    /// poll keeps things consistent). Soft-fails on a protected/elevated process we can't kill without
    /// admin, surfacing a brief message rather than throwing.</summary>
    [RelayCommand]
    private void ConfirmEndTask() {
        ConfirmVisible = false;
        var row = SelectedRow;
        if (row is null)
            return;

        try {
            using var process = Process.GetProcessById(row.Pid);
            process.Kill();
            if (!Apps.Remove(row) && !Background.Remove(row))
                WindowsProcesses.Remove(row);
            SelectedRow = null;
            ActionMessage = "";
        } catch {
            // ArgumentException (already exited) or Win32Exception (access denied without elevation).
            ActionMessage = $"Couldn't end {row.Name}";
        }
    }

    /// <summary>
    /// The page's keyboard shortcuts. The confirmation overlay is modal for this page, so while it is up
    /// it answers Enter and Esc and swallows everything else — the same rule the shell applies for the
    /// Help modal, one level down.
    /// </summary>
    public ShortcutScope Scope => ShortcutScope.Processes;

    public bool HandleShortcut(ShortcutId id) {
        if (ConfirmVisible) {
            switch (id) {
                case ShortcutId.Activate: ConfirmEndTask(); return true;
                case ShortcutId.Escape: CancelEndTask(); return true;
                default: return true;
            }
        }

        switch (id) {
            case ShortcutId.FocusFilter:
                FilterFocusRequested?.Invoke();
                return true;

            // Leave Delete unconsumed with nothing selected, so it isn't silently swallowed.
            case ShortcutId.EndTask when HasSelection:
                RequestEndTask();
                return true;

            case ShortcutId.SortAscending:
                return SetSortDirection(ascending: true);

            case ShortcutId.SortDescending:
                return SetSortDirection(ascending: false);

            // Esc clears the filter first; with nothing to clear it falls through to the shell.
            case ShortcutId.Escape when FilterText.Length > 0:
                ClearFilter();
                return true;

            default:
                return false;
        }
    }

    /// <summary>Toolbar Refresh: re-sample the summary totals and reload the list once, even while paused.</summary>
    public void Refresh() {
        _service.RefreshAll();
        _ = LoadAsync();
    }

    /// <summary>Pauses/resumes the list-polling timer, driven by the toolbar Live pill. The shared summary
    /// sampling is paused separately by the shell via <see cref="SystemMetricsService.Pause"/>.</summary>
    public void SetLive(bool live) {
        if (live)
            _timer.Start();
        else
            _timer.Stop();
    }

    /// <summary>Stops the timer and unsubscribes from the shared metrics. Safe to call more than once.</summary>
    public void Dispose() {
        _timer.Stop();
        _timer.Tick -= OnTick;
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
    }
}
