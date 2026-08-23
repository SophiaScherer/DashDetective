using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Shared.Completion;
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
public partial class ProcessesViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, IActivatablePage, ISelfScrollingPage, IShortcutTarget, IDisposable {
    /// <summary>Poll cadence. Enumerating every process (with per-process window/responding probes) is
    /// heavier than a single counter, so it polls slower than the Dashboard's 1 Hz samplers — close to
    /// Task Manager's own refresh.</summary>
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(2);

    private readonly DispatcherTimer _timer;
    private readonly OverlapGuard _loadGuard = new();

    // System-wide CPU% / Memory% for the summary strip — the same readings the Dashboard shows, from the
    // shared SystemMetricsService (so there's one sampler across all tabs).
    private readonly SystemMetricsService _service;
    private readonly IProcessSnapshotProvider _snapshots;
    private readonly IProcessInterop _interop;
    private readonly MetricSubscriptions _subscriptions;
    private readonly SamplingGate _gate;

    // Sort state: which column + direction. Sorting applies within each group; Apps stay above
    // Background. Defaults to Name ascending (matching the initial list order).
    private ProcessSortKey _sortKey = ProcessSortKey.Name;
    private bool _ascending = true;
    private readonly SortColumn<ProcessSortKey>[] _sortColumns;

    /// <summary>The last snapshot, kept so a header click can re-sort immediately without waiting for
    /// the next poll.</summary>
    private IReadOnlyList<ProcessInfo> _lastSnapshot = Array.Empty<ProcessInfo>();

    /// <summary>The processes from the last poll, for universal search to match against. Reading what
    /// this page already has in hand means searching costs no extra enumeration; it is at most one poll
    /// (two seconds) stale, which is the same list the user is looking at.</summary>
    public IReadOnlyList<ProcessInfo> Snapshot => _lastSnapshot;

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

    /// <summary>Task Manager's third group, updated in place: system/service processes outside the
    /// interactive session on Windows, kernel threads and <c>system.slice</c> units on Linux. Captioned by
    /// <see cref="ProcessGroupNames"/>, which is the only part of it that differs per platform.</summary>
    public ObservableCollection<ProcessRow> WindowsProcesses { get; } = new();

    // Clickable column headers.
    public SortColumn<ProcessSortKey> NameSort { get; }
    public SortColumn<ProcessSortKey> PidSort { get; }
    public SortColumn<ProcessSortKey> StatusSort { get; }
    public SortColumn<ProcessSortKey> CpuSort { get; }
    public SortColumn<ProcessSortKey> MemorySort { get; }
    public SortColumn<ProcessSortKey> DiskSort { get; }
    public SortColumn<ProcessSortKey> GpuSort { get; }

    /// <summary>Group header caption for the Apps section (e.g. "Apps · 6").</summary>
    [ObservableProperty] private string _appsHeader = "Apps";

    /// <summary>Group header caption for the Background section (e.g. "Background processes · 127").</summary>
    [ObservableProperty] private string _backgroundHeader = "Background processes";

    /// <summary>Group header caption for the third section — "Windows processes · 150", or "System
    /// processes · 150" on Linux.</summary>
    [ObservableProperty] private string _windowsHeader = ProcessGroupNames.SystemHeader;

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

    // ----- Responsive table columns -----

    // Starts unconstrained so the table shows every column on the first layout pass, before the view
    // has reported a width; it narrows from there if the real width turns out to be smaller.
    private double _tableWidth = double.PositiveInfinity;
    private int _visibleColumns = ProcessTableLayout.Minimums.Length;

    /// <summary>The table's ColumnDefinitions at the current width. The sticky header and the shared
    /// row template both bind to this, so they cannot fall out of alignment.</summary>
    public string ColumnLayout => ProcessTableLayout.Definitions(_tableWidth);

    public bool ShowStatusColumn => ProcessTableLayout.ShowStatus(_tableWidth);

    public bool ShowDiskColumn => ProcessTableLayout.ShowDisk(_tableWidth);

    public bool ShowGpuColumn => ProcessTableLayout.ShowGpu(_tableWidth);

    /// <summary>Reports the width the table is laid out in; the view pushes this because there is no
    /// converter-free path from an element's bounds to a view model. Only re-notifies when the column
    /// set actually changes, so dragging the window doesn't churn bindings on every pixel.</summary>
    public void SetTableWidth(double width) {
        if (!double.IsFinite(width) || width <= 0)
            return;

        _tableWidth = width;
        var visible = ProcessTableLayout.VisibleCount(width);
        if (visible == _visibleColumns)
            return;

        _visibleColumns = visible;
        OnPropertyChanged(nameof(ColumnLayout));
        OnPropertyChanged(nameof(ShowStatusColumn));
        OnPropertyChanged(nameof(ShowDiskColumn));
        OnPropertyChanged(nameof(ShowGpuColumn));
    }

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

    /// <summary>The running process name the filter should complete to, ghosted after the caret for Tab
    /// to accept. Null when nothing matches, or when the candidates disagree past what is typed.</summary>
    [ObservableProperty] private string? _filterCompletion;

    partial void OnFilterTextChanged(string value) {
        RebuildVisibleRows();
        UpdateFilterCompletion();
    }

    // Recomputed on each keystroke and each poll, over the snapshot already in hand — a few hundred
    // names is nothing next to the tree rebuild that runs alongside it.
    private void UpdateFilterCompletion() {
        var names = new List<string>(_lastSnapshot.Count);
        foreach (var process in _lastSnapshot)
            names.Add(process.Name);

        FilterCompletion = PrefixCompleter.Complete(FilterText, names);
    }

    /// <summary>Clears the filter (the box's × button, and Esc while the box has content).</summary>
    [RelayCommand]
    private void ClearFilter() => FilterText = "";

    /// <summary>Raised when the focus-filter shortcut fires, so the view can put the caret in the box.
    /// UI-only; carries no state — the same view/view-model seam the File Explorer uses for scrolling.</summary>
    public event Action? FilterFocusRequested;

    /// <summary>Raised after <see cref="Reveal"/> narrows the list, so the view can reset the table's
    /// scroll to the top and the revealed row is on screen. UI-only, like the focus request above.</summary>
    public event Action? ScrollToTopRequested;

    /// <summary>
    /// Points the page at one process, for a jump from universal search: filters to its name, expands
    /// whatever it is nested under, and selects its row.
    ///
    /// Filtering by name rather than by PID is deliberate — a multi-process app collapses into one entry
    /// here, so the user sees the whole group they searched for and not a lone helper torn out of it.
    /// </summary>
    public void Reveal(int pid) {
        var path = new List<ProcessNode>();
        if (!TryFindPath(_lastRoots, pid, path))
            return;

        // Reveal the ancestors before filtering so the single rebuild below shows the finished state.
        for (var i = 0; i < path.Count - 1; i++)
            _expandedPids.Add(path[i].Info.Pid);

        // Assigning the filter rebuilds the rows; only rebuild by hand when it was already this term.
        var name = path[^1].Info.Name;
        if (FilterText == name)
            RebuildVisibleRows();
        else
            FilterText = name;

        SelectByPid(pid);
        ScrollToTopRequested?.Invoke();
    }

    /// <summary>Depth-first search for the node with this PID, recording the nodes walked through to
    /// reach it (so <see cref="Reveal"/> can expand them). Internal only so it is testable without a
    /// dispatcher — the rest of this class needs a live timer to construct.</summary>
    internal static bool TryFindPath(IReadOnlyList<ProcessNode> nodes, int pid, List<ProcessNode> path) {
        foreach (var node in nodes) {
            path.Add(node);
            if (node.Info.Pid == pid || TryFindPath(node.Children, pid, path))
                return true;
            path.RemoveAt(path.Count - 1);
        }

        return false;
    }

    // Selects the row for a PID across the three groups. A PID with no row (its group is filtered out,
    // or it exited between the search and the jump) simply leaves the selection alone.
    private void SelectByPid(int pid) {
        foreach (var group in new[] { Apps, Background, WindowsProcesses })
            foreach (var row in group)
                if (row.Pid == pid) {
                    SelectRow(row);
                    return;
                }
    }

    public ProcessesViewModel(SystemMetricsService service)
        : this(service, IProcessInterop.ForCurrentPlatform()) { }

    private ProcessesViewModel(SystemMetricsService service, IProcessInterop interop)
        : this(service, IProcessSnapshotProvider.ForCurrentPlatform(interop), interop) { }

    /// <summary>Test seam: the same page over explicit providers. The public ctor resolves the real ones,
    /// so the shell still builds this exactly as before.</summary>
    internal ProcessesViewModel(SystemMetricsService service, IProcessSnapshotProvider snapshots,
                                IProcessInterop interop) {
        _snapshots = snapshots;
        _interop = interop;

        _service = service;
        NameSort = new SortColumn<ProcessSortKey>(ProcessSortKey.Name, OnSort);
        PidSort = new SortColumn<ProcessSortKey>(ProcessSortKey.Pid, OnSort);
        StatusSort = new SortColumn<ProcessSortKey>(ProcessSortKey.Status, OnSort);
        CpuSort = new SortColumn<ProcessSortKey>(ProcessSortKey.Cpu, OnSort);
        MemorySort = new SortColumn<ProcessSortKey>(ProcessSortKey.Memory, OnSort);
        DiskSort = new SortColumn<ProcessSortKey>(ProcessSortKey.Disk, OnSort);
        GpuSort = new SortColumn<ProcessSortKey>(ProcessSortKey.Gpu, OnSort);
        _sortColumns = new[] {
            NameSort, PidSort, StatusSort, CpuSort, MemorySort, DiskSort, GpuSort,
        };
        UpdateSortIndicators();

        // The summary CPU%/Memory% come from the shared service, subscribed on activation rather than here
        // (the feeds are ref-counted, so staying subscribed off screen keeps them sampling); attaching
        // replays the latest value at once.
        _subscriptions = new MetricSubscriptions(
            () => _service.SubscribeCpu(OnCpuTotal, OnCpuTotalFailed),
            () => _service.SubscribeMemory(OnMemoryTotal, OnMemoryTotalFailed));

        // Built FIRST, before anything that reads it: every load captures _gate.Token, so a load started
        // above this line would dereference a null field and its soft-fail would empty the page. The gate
        // starts idle and fires no callback until a transition, so building it early costs nothing.
        _gate = new SamplingGate(ApplySampling);

        // One list load here even though the page is not on screen yet: universal search reads Snapshot,
        // and a tab the user has not opened would otherwise offer no processes at all.
        _ = LoadAsync();

        // The timer is not started here: the gate runs it only while the page is on screen and the Live
        // pill is on.
        _timer = new DispatcherTimer { Interval = SampleInterval };
        _timer.Tick += OnTick;
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
    /// <summary>Internal rather than private so a test can await the poll the ctor fires and forgets.</summary>
    internal async Task LoadAsync() {
        using var run = _loadGuard.TryEnter();
        if (run is null)
            return;

        var token = _gate.Token;
        try {
            var processes = await _snapshots.GetAsync(token);
            // Awaited on the UI thread, so the continuation resumes there — safe to touch collections.
            token.ThrowIfCancellationRequested();
            _lastSnapshot = processes;
            ApplySnapshot(processes);
        } catch when (token.IsCancellationRequested) {
            // Left mid-read: cancelled, or failed once the user had already gone. Either way the
            // emptying fallback below must NOT run — it would blank the list they come back to.
        } catch {
            _lastSnapshot = Array.Empty<ProcessInfo>();
            _lastRoots = Array.Empty<ProcessNode>();
            Apps.Clear();
            Background.Clear();
            WindowsProcesses.Clear();
            AppsHeader = "Apps";
            BackgroundHeader = "Background processes";
            WindowsHeader = ProcessGroupNames.SystemHeader;
            TotalProcessesText = "0";
            ProcessBreakdownText = "";
            ThreadsText = "0";
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
        UpdateFilterCompletion();
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

        // The summary strip describes the machine, so it counts every process; the lists and their group
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
        WindowsHeader =
            $"{ProcessGroupNames.SystemHeader} · {visibleWindows.Count.ToString(CultureInfo.InvariantCulture)}";

        // Total threads span every process, not just the top-level entries.
        var totalThreads = 0;
        foreach (var info in _lastSnapshot)
            totalThreads += info.ThreadCount;

        var (apps, background, windows) = CountByGroup(_lastSnapshot, _lastRoots);
        TotalProcessesText = _lastSnapshot.Count.ToString(CultureInfo.InvariantCulture);
        ProcessBreakdownText = $"{apps.ToString(CultureInfo.InvariantCulture)} apps · " +
                               $"{background.ToString(CultureInfo.InvariantCulture)} background · " +
                               $"{windows.ToString(CultureInfo.InvariantCulture)} {ProcessGroupNames.SystemLabel}";
        ThreadsText = totalThreads.ToString("N0", CultureInfo.InvariantCulture);
    }

    /// <summary>Splits every live process across the three groups for the summary breakdown. A collapsed
    /// app's helpers are counted in the group their root is displayed under (so Edge's helpers land in Apps
    /// with Edge, not in Background on their own), and a process the tree somehow didn't reach falls back to
    /// its own category — so the three counts always sum to <paramref name="processes"/>. Internal only so
    /// it is testable without a dispatcher, like <see cref="TryFindPath"/>.</summary>
    internal static (int Apps, int Background, int Windows) CountByGroup(
        IReadOnlyList<ProcessInfo> processes, IReadOnlyList<ProcessNode> roots) {
        var groupOf = new Dictionary<int, ProcessCategory>(processes.Count);
        foreach (var root in roots)
            MapToGroup(root, root.Aggregate.Category, groupOf);

        int apps = 0, background = 0, windows = 0;
        foreach (var info in processes) {
            switch (groupOf.TryGetValue(info.Pid, out var group) ? group : info.Category) {
                case ProcessCategory.App: apps++; break;
                case ProcessCategory.Windows: windows++; break;
                default: background++; break;
            }
        }

        return (apps, background, windows);
    }

    /// <summary>Records a group root and all its collapsed descendants under the root's category.</summary>
    private static void MapToGroup(ProcessNode node, ProcessCategory category, Dictionary<int, ProcessCategory> into) {
        into[node.Info.Pid] = category;
        foreach (var child in node.Children)
            MapToGroup(child, category, into);
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

    /// <summary>Pauses/resumes the page's sampling, driven by the toolbar Live pill. The shared summary feeds
    /// are also paused service-wide by the shell via <see cref="SystemMetricsService.Pause"/>.</summary>
    public void SetLive(bool live) => _gate.Live = live;

    /// <summary>Starts/stops the page's sampling as it comes on and off screen.</summary>
    public void SetActive(bool active) => _gate.Active = active;

    /// <summary>Runs or halts the summary subscriptions and the list-polling timer — the gate's composed
    /// answer, so it reflects the Live pill and the tab's visibility at once.</summary>
    private void ApplySampling(bool running) {
        if (running) {
            _subscriptions.Attach();
            _timer.Start();

            // Any time away leaves the list stale, so reload now rather than showing the old rows until
            // the first tick.
            _ = LoadAsync();
        } else {
            _subscriptions.Detach();
            _timer.Stop();
        }
    }

    /// <summary>Stops the timer and unsubscribes from the shared metrics. Safe to call more than once.</summary>
    public void Dispose() {
        _gate.Dispose();
        _timer.Stop();
        _timer.Tick -= OnTick;
        _subscriptions.Dispose();
    }

    /// <summary>Shows the native Properties dialog for the selected process. Lives here rather than in
    /// the view because the view has no injection point (the ViewLocator builds views by name with a
    /// parameterless ctor); the code-behind's job is only to fetch the window handle.</summary>
    internal void ShowProperties(IntPtr owner, int pid) => _interop.ShowProperties(owner, pid);
}
