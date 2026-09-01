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
using System.Globalization;
using System.Linq;
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
public partial class ProcessesViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, IActivatablePage, ISelfScrollingPage, IShortcutTarget, IReorderablePage, IDisposable {
    /// <summary>The order of the summary tiles above the table, bound two-way to their strip.</summary>
    public SavedOrder Summary { get; } = new("processes.summary");

    public IEnumerable<SavedOrder> SavedOrders => [Summary];

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
    private readonly IProcessTerminator _terminator;
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

    /// <summary>Groups the user has folded shut. Their rows stay in their collections — a collapsed
    /// group still counts, still filters and still selects; only the list is hidden.</summary>
    private readonly HashSet<ProcessCategory> _collapsedGroups = new();

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
    [ObservableProperty] private string _totalProcessesText = Placeholders.NoReading;

    /// <summary>Per-group breakdown under the total (e.g. "10 apps · 310 background").</summary>
    [ObservableProperty] private string _processBreakdownText = "";

    /// <summary>System-wide CPU utilisation, whole percent (e.g. "12%").</summary>
    [ObservableProperty] private string _cpuUsageText = "0%";

    /// <summary>System-wide physical-memory usage, whole percent (e.g. "49%").</summary>
    [ObservableProperty] private string _memoryUsageText = "0%";

    /// <summary>Total thread count across all processes (e.g. "2,418").</summary>
    [ObservableProperty] private string _threadsText = Placeholders.NoReading;

    /// <summary>Whether the first enumeration has come back. Until it has, the table shows placeholder
    /// rows: an empty list is indistinguishable from a machine running no processes, which is a claim
    /// this page should not make before it has looked.</summary>
    [ObservableProperty] private bool _hasLoaded;

    /// <summary>How many placeholder rows to draw while waiting. A fixed count — there is no data yet
    /// to derive one from, and this is roughly a screenful.</summary>
    public static IReadOnlyList<int> SkeletonRows { get; } = Enumerable.Range(0, 14).ToArray();

    // ----- Table columns -----

    /// <summary>The columns left to right. The user drags the header cells to change this; it is
    /// persisted by column name, so a release that adds or drops a column cannot re-point a saved
    /// order at different ones.</summary>
    private readonly List<ProcessColumnId> _columnOrder = new(ProcessColumns.DefaultOrder);

    /// <summary>Raised when something this page persists settles: the column order, the remember
    /// toggles, and — only while the matching toggle is on — the collapsed sections and the sort.
    /// Deliberately NOT raised per pointer move during a column drag: that previews live, and saving
    /// every frame of it would write the settings file dozens of times for one gesture.</summary>
    public event Action? PreferencesChanged;

    /// <summary>The column order, for the shell to seed from settings and read back. Assigning
    /// resolves what was saved against the columns the table declares now.</summary>
    public IReadOnlyList<ProcessColumnId> ColumnOrder {
        get => _columnOrder;
        set {
            _columnOrder.Clear();
            _columnOrder.AddRange(ProcessColumnOrder.Resolve(value));
            NotifyColumnsChanged();
        }
    }

    /// <summary>The table's ColumnDefinitions in the current order. The sticky header and the shared
    /// row template both bind to this, so they cannot fall out of alignment. Every column is always
    /// present — a table too narrow for them scrolls sideways rather than dropping any.</summary>
    public string ColumnLayout => ProcessTableLayout.Definitions(_columnOrder);

    // Where each cell sits. The header cells and the row template bind their Grid.Column to these, so a
    // reorder moves the cells rather than rebuilding the table.
    public int NameColumn => _columnOrder.IndexOf(ProcessColumnId.Name);
    public int PidColumn => _columnOrder.IndexOf(ProcessColumnId.Pid);
    public int StatusColumn => _columnOrder.IndexOf(ProcessColumnId.Status);
    public int CpuColumn => _columnOrder.IndexOf(ProcessColumnId.Cpu);
    public int MemoryColumn => _columnOrder.IndexOf(ProcessColumnId.Memory);
    public int DiskColumn => _columnOrder.IndexOf(ProcessColumnId.Disk);
    public int GpuColumn => _columnOrder.IndexOf(ProcessColumnId.Gpu);

    /// <summary>The column shown at <paramref name="index"/>, for the header's drag hit-testing.</summary>
    public ProcessColumnId ColumnAt(int index) => _columnOrder[Math.Clamp(index, 0, _columnOrder.Count - 1)];

    /// <summary>Moves a column to a new position, for the header drag. Returns false when nothing
    /// moved, so a wobble mid-drag doesn't re-notify every frame. Silent by design — the drag calls
    /// this on each move to preview the result and <see cref="CommitColumnOrder"/> once on release.</summary>
    public bool MoveColumn(ProcessColumnId id, int target) {
        // The pinned column neither moves nor is displaced: index 0 is not a drop target.
        if (id == ProcessColumns.Pinned)
            return false;

        var from = _columnOrder.IndexOf(id);
        target = Math.Clamp(target, 1, _columnOrder.Count - 1);
        if (from < 1 || from == target)
            return false;

        _columnOrder.RemoveAt(from);
        _columnOrder.Insert(target, id);
        NotifyColumnsChanged();
        return true;
    }

    /// <summary>Reports the order settled on, so the shell persists it once per gesture.</summary>
    public void CommitColumnOrder() => PreferencesChanged?.Invoke();

    /// <summary>Puts the columns back the way the table ships, for the Reset control.</summary>
    [RelayCommand]
    public void ResetColumnOrder() {
        if (_columnOrder.SequenceEqual(ProcessColumns.DefaultOrder))
            return;

        _columnOrder.Clear();
        _columnOrder.AddRange(ProcessColumns.DefaultOrder);
        NotifyColumnsChanged();
        CommitColumnOrder();
    }

    private void NotifyColumnsChanged() {
        OnPropertyChanged(nameof(ColumnLayout));
        OnPropertyChanged(nameof(NameColumn));
        OnPropertyChanged(nameof(PidColumn));
        OnPropertyChanged(nameof(StatusColumn));
        OnPropertyChanged(nameof(CpuColumn));
        OnPropertyChanged(nameof(MemoryColumn));
        OnPropertyChanged(nameof(DiskColumn));
        OnPropertyChanged(nameof(GpuColumn));
    }

    // ----- Selection + actions -----

    /// <summary>The selected PIDs. Authoritative — rows are transient (the keyed diff recreates them),
    /// so like <see cref="_expandedPids"/> the set is what survives a poll, a re-sort and a filter.</summary>
    private readonly HashSet<int> _selectedPids = new();

    /// <summary>Where a Shift-click measures its range from: the row a plain or Ctrl-click last landed
    /// on. Zero when there is nothing to measure from.</summary>
    private int _anchorPid;

    /// <summary>The primary row — the last one clicked. The selection can hold many; the things that can
    /// only act on one (Properties' shell dialog) act on this.</summary>
    [ObservableProperty] private ProcessRow? _selectedRow;

    /// <summary>Whether anything is selected — enables the End task and Properties buttons.</summary>
    public bool HasSelection => _selectedPids.Count > 0;

    /// <summary>How many processes are selected.</summary>
    public int SelectionCount => _selectedPids.Count;

    /// <summary>The selection caption beside the action buttons (e.g. "3 selected").</summary>
    public string SelectionText => $"{SelectionCount.ToString(CultureInfo.InvariantCulture)} selected";

    /// <summary>Whether the primary row can be expanded — enables the context menu's expand item.</summary>
    public bool SelectedHasChildren => SelectedRow?.HasChildren == true;

    // Whether each group is folded shut, and the ▾/▸ its header shows.
    public bool AppsCollapsed => _collapsedGroups.Contains(ProcessCategory.App);
    public bool BackgroundCollapsed => _collapsedGroups.Contains(ProcessCategory.Background);
    public bool WindowsCollapsed => _collapsedGroups.Contains(ProcessCategory.Windows);

    public string AppsChevron => ChevronFor(AppsCollapsed);
    public string BackgroundChevron => ChevronFor(BackgroundCollapsed);
    public string WindowsChevron => ChevronFor(WindowsCollapsed);

    private static string ChevronFor(bool collapsed) => collapsed ? "▸" : "▾";

    /// <summary>Whether folded sections come back after a restart. Off by default: the fold is a
    /// glance-at-something gesture more often than a preference, so it starts fresh unless the user
    /// says otherwise — the shape File Explorer's "Show hidden" option has.</summary>
    [ObservableProperty] private bool _rememberCollapsedGroups;

    /// <summary>Whether the sort column and direction come back after a restart. Off by default, for
    /// the same reason as <see cref="RememberCollapsedGroups"/>.</summary>
    [ObservableProperty] private bool _rememberSort;

    partial void OnRememberCollapsedGroupsChanged(bool value) => PreferencesChanged?.Invoke();

    partial void OnRememberSortChanged(bool value) => PreferencesChanged?.Invoke();

    /// <summary>The folded sections, for the shell to seed from settings and read back.</summary>
    public IReadOnlyCollection<ProcessCategory> CollapsedGroups {
        get => _collapsedGroups;
        set {
            _collapsedGroups.Clear();
            foreach (var category in value)
                _collapsedGroups.Add(category);
            NotifyGroupsChanged();
        }
    }

    /// <summary>The active sort column, for the shell to seed from settings and read back. Assigning
    /// re-sorts but stays quiet, so seeding a saved sort does not write it straight back.</summary>
    public ProcessSortKey SortKey {
        get => _sortKey;
        set {
            if (_sortKey == value)
                return;
            _sortKey = value;
            UpdateSortIndicators();
            RebuildVisibleRows();
        }
    }

    /// <summary>The active sort direction. Assigning is quiet, like <see cref="SortKey"/>.</summary>
    public bool SortAscending {
        get => _ascending;
        set {
            if (_ascending == value)
                return;
            _ascending = value;
            UpdateSortIndicators();
            RebuildVisibleRows();
        }
    }

    /// <summary>Folds a group shut, or opens it again — its header. Session state unless the user has
    /// asked for it to be remembered; nothing is re-read, only hidden.</summary>
    public void ToggleGroup(ProcessCategory category) {
        if (!_collapsedGroups.Remove(category))
            _collapsedGroups.Add(category);

        NotifyGroupsChanged();
        if (RememberCollapsedGroups)
            PreferencesChanged?.Invoke();
    }

    private void NotifyGroupsChanged() {
        OnPropertyChanged(nameof(AppsCollapsed));
        OnPropertyChanged(nameof(BackgroundCollapsed));
        OnPropertyChanged(nameof(WindowsCollapsed));
        OnPropertyChanged(nameof(AppsChevron));
        OnPropertyChanged(nameof(BackgroundChevron));
        OnPropertyChanged(nameof(WindowsChevron));
    }

    // The group headers' select-all boxes: ticked when the group holds every visible row, dashed when
    // it holds only part of it. Two booleans rather than one nullable because they drive style classes.
    public bool AppsAllSelected => IsAllSelected(Apps);
    public bool AppsSomeSelected => IsSomeSelected(Apps);
    public bool BackgroundAllSelected => IsAllSelected(Background);
    public bool BackgroundSomeSelected => IsSomeSelected(Background);
    public bool WindowsAllSelected => IsAllSelected(WindowsProcesses);
    public bool WindowsSomeSelected => IsSomeSelected(WindowsProcesses);

    /// <summary>Whether the End-task confirmation overlay is showing.</summary>
    [ObservableProperty] private bool _confirmVisible;

    /// <summary>The confirmation prompt for the process being ended.</summary>
    [ObservableProperty] private string _confirmText = "";

    /// <summary>Transient feedback after an action (e.g. a soft-failed End task). Cleared on the next
    /// selection or successful action.</summary>
    [ObservableProperty] private string _actionMessage = "";


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
                                IProcessInterop interop, IProcessTerminator? terminator = null) {
        _snapshots = snapshots;
        _interop = interop;
        _terminator = terminator ?? new ProcessTerminator();

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

    /// <summary>On a CPU sampler failure, report no reading. NOT 0%: the channel stops polling after a
    /// failure, so a confident "0%" would sit there for the rest of the session claiming an idle CPU.
    /// This is the same feed the Dashboard, Performance and Network tabs render as "—".</summary>
    private void OnCpuTotalFailed() => CpuUsageText = Placeholders.NoReading;

    /// <summary>On a memory sampler failure, report no reading — see <see cref="OnCpuTotalFailed"/>.</summary>
    private void OnMemoryTotalFailed() => MemoryUsageText = Placeholders.NoReading;

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
            // Not "0": the enumeration failed, so the count is unknown, not zero — the same reading
            // the CPU and memory feeds already report when their sampler gives up.
            TotalProcessesText = Placeholders.NoReading;
            ProcessBreakdownText = "";
            ThreadsText = Placeholders.NoReading;
        } finally {
            // Set on success AND on failure, never on cancellation. It marks "this page has an answer",
            // and a page that failed has one — an empty list, honestly labelled. Leaving it false would
            // sit under the skeleton for the rest of the session pretending to still be working.
            if (!token.IsCancellationRequested)
                HasLoaded = true;
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

    /// <summary>Drops selected PIDs that no longer name a live process, so the set doesn't accumulate
    /// stale entries across polls — the same job <see cref="PruneExpanded"/> does for expansion.</summary>
    private void PruneSelection() {
        if (_selectedPids.Count == 0)
            return;

        var live = new HashSet<int>(_lastSnapshot.Count);
        foreach (var info in _lastSnapshot)
            live.Add(info.Pid);

        _selectedPids.IntersectWith(live);
        if (!_selectedPids.Contains(_anchorPid))
            _anchorPid = 0;
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

        // Selection is pruned against the LIVE processes, not the visible rows: a row hidden by the
        // filter is still a running process the user picked, and narrowing the list must not silently
        // drop it. Only an exited process leaves the selection.
        PruneSelection();
        ApplySelection();

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
        if (RememberSort)
            PreferencesChanged?.Invoke();
    }

    /// <summary>Sets the direction on whichever column is already sorted, leaving the column itself
    /// alone (Alt+↑ / Alt+↓). Asking for the direction already in effect is a no-op, but still counts as
    /// handled so the key isn't passed on to scroll the list.</summary>
    public bool SetSortDirection(bool ascending) {
        if (_ascending != ascending) {
            _ascending = ascending;
            UpdateSortIndicators();
            RebuildVisibleRows();
            if (RememberSort)
                PreferencesChanged?.Invoke();
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

    /// <summary>Selects one row, dropping whatever was selected before. Driven from the view code-behind
    /// on tap, like File Explorer's row selection.</summary>
    public void SelectRow(ProcessRow row) => SelectRow(row, extend: false, range: false);

    /// <summary>
    /// Selects a row the way the modifier keys ask for: plain replaces the selection, Ctrl
    /// (<paramref name="extend"/>) adds or removes just this row, Shift (<paramref name="range"/>) takes
    /// everything between the anchor and this row.
    /// </summary>
    public void SelectRow(ProcessRow row, bool extend, bool range) {
        if (range && _anchorPid != 0) {
            SelectRange(_anchorPid, row.Pid);
        } else if (extend) {
            if (!_selectedPids.Remove(row.Pid))
                _selectedPids.Add(row.Pid);
            _anchorPid = row.Pid;
        } else {
            _selectedPids.Clear();
            _selectedPids.Add(row.Pid);
            _anchorPid = row.Pid;
        }

        SelectedRow = row;
        ApplySelection();
        ActionMessage = "";
    }

    /// <summary>Adds or removes one row, leaving the rest of the selection alone — the row checkbox, and
    /// what a Ctrl-click does.</summary>
    public void ToggleSelected(ProcessRow row) => SelectRow(row, extend: true, range: false);

    /// <summary>Replaces the selection with every visible row between two PIDs inclusive, in display
    /// order across all three groups — a Shift-click, and what a drag down the list tracks.</summary>
    public void SelectRange(int fromPid, int toPid) {
        var rows = VisibleRows();
        var from = rows.FindIndex(row => row.Pid == fromPid);
        var to = rows.FindIndex(row => row.Pid == toPid);
        if (from < 0 || to < 0)
            return;

        if (from > to)
            (from, to) = (to, from);

        _selectedPids.Clear();
        for (var i = from; i <= to; i++)
            _selectedPids.Add(rows[i].Pid);

        ApplySelection();
        ActionMessage = "";
    }

    /// <summary>Selects or clears every visible row in one group — the select-all checkbox on its
    /// header. Only what the filter has left on screen, which is what the header's count says too.</summary>
    public void SetGroupSelected(ProcessCategory category, bool selected) {
        foreach (var row in GroupFor(category)) {
            if (selected)
                _selectedPids.Add(row.Pid);
            else
                _selectedPids.Remove(row.Pid);
        }

        ApplySelection();
        ActionMessage = "";
    }

    /// <summary>Drops the whole selection.</summary>
    public void ClearSelection() {
        if (_selectedPids.Count == 0)
            return;

        _selectedPids.Clear();
        _anchorPid = 0;
        SelectedRow = null;
        ApplySelection();
    }

    /// <summary>The selected PIDs, for the actions that operate on all of them.</summary>
    public IReadOnlyCollection<int> SelectedPids => _selectedPids;

    private ObservableCollection<ProcessRow> GroupFor(ProcessCategory category) => category switch {
        ProcessCategory.App => Apps,
        ProcessCategory.Windows => WindowsProcesses,
        _ => Background,
    };

    /// <summary>Every row on screen, top to bottom, so a range spans the group boundaries the way the
    /// eye reads them.</summary>
    private List<ProcessRow> VisibleRows() {
        var rows = new List<ProcessRow>(Apps.Count + Background.Count + WindowsProcesses.Count);
        rows.AddRange(Apps);
        rows.AddRange(Background);
        rows.AddRange(WindowsProcesses);
        return rows;
    }

    /// <summary>Pushes the selection set onto the rows and re-notifies everything derived from it. The
    /// set leads and the rows follow, so a row recreated by the next poll comes back selected.</summary>
    private void ApplySelection() {
        foreach (var group in new[] { Apps, Background, WindowsProcesses })
            foreach (var row in group)
                row.IsSelected = _selectedPids.Contains(row.Pid);

        // The primary row can be left pointing at a process that is no longer in the selection at all
        // (a Ctrl-click that removed it); fall back to any row still selected so Properties has a target.
        if (SelectedRow is not null && !_selectedPids.Contains(SelectedRow.Pid))
            SelectedRow = FirstSelected();

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(SelectedHasChildren));
        OnPropertyChanged(nameof(AppsAllSelected));
        OnPropertyChanged(nameof(AppsSomeSelected));
        OnPropertyChanged(nameof(BackgroundAllSelected));
        OnPropertyChanged(nameof(BackgroundSomeSelected));
        OnPropertyChanged(nameof(WindowsAllSelected));
        OnPropertyChanged(nameof(WindowsSomeSelected));
    }

    private ProcessRow? FirstSelected() {
        foreach (var row in VisibleRows())
            if (row.IsSelected)
                return row;
        return null;
    }

    /// <summary>Whether every visible row in a group is selected — what the header's checkbox toggles
    /// against. Read by the view instead of the box's own state, which has already advanced by the time
    /// the click handler runs.</summary>
    public bool IsGroupFullySelected(ProcessCategory category) => IsAllSelected(GroupFor(category));

    /// <summary>Whether every visible row in a group is selected. An empty group reads as unselected
    /// rather than "all", so its box isn't ticked with nothing under it.</summary>
    private bool IsAllSelected(ObservableCollection<ProcessRow> group) =>
        group.Count > 0 && SelectedIn(group) == group.Count;

    /// <summary>Whether a group holds some but not all of its visible rows.</summary>
    private bool IsSomeSelected(ObservableCollection<ProcessRow> group) {
        var selected = SelectedIn(group);
        return selected > 0 && selected < group.Count;
    }

    private int SelectedIn(ObservableCollection<ProcessRow> group) {
        var selected = 0;
        foreach (var row in group)
            if (_selectedPids.Contains(row.Pid))
                selected++;
        return selected;
    }

    /// <summary>End task button: shows the confirmation overlay for everything selected (killing a
    /// process is destructive, so it isn't done on a single click).</summary>
    [RelayCommand]
    private void RequestEndTask() {
        if (!HasSelection)
            return;

        ConfirmText = SelectionCount == 1
            ? $"End “{NameOf(_selectedPids.First())}”? Any unsaved work in this process will be lost."
            : $"End these {SelectionCount.ToString(CultureInfo.InvariantCulture)} processes? " +
              "Any unsaved work in them will be lost.";
        ConfirmVisible = true;
    }

    /// <summary>A process's name for a message. Visible rows first, falling back to the snapshot — a
    /// selected process the filter is hiding still has to be nameable.</summary>
    private string NameOf(int pid) {
        foreach (var row in VisibleRows())
            if (row.Pid == pid)
                return row.Name;

        foreach (var info in _lastSnapshot)
            if (info.Pid == pid)
                return info.Name;

        return pid.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Dismisses the confirmation overlay without ending anything.</summary>
    [RelayCommand]
    private void CancelEndTask() => ConfirmVisible = false;

    /// <summary>Confirms the End task: terminates every selected process and drops the rows immediately
    /// (the next poll keeps things consistent). One protected or already-exited process does not stop
    /// the rest — they are counted and reported together.
    ///
    /// It works over the selected PIDs rather than the visible rows, because the selection survives the
    /// filter: a process the user picked and then filtered out of sight is still one they asked to end.</summary>
    [RelayCommand]
    private void ConfirmEndTask() {
        ConfirmVisible = false;
        if (_selectedPids.Count == 0)
            return;

        var pids = new List<int>(_selectedPids);
        var failed = new List<int>();
        foreach (var pid in pids)
            if (!_terminator.TryEnd(pid))
                failed.Add(pid);

        // Named before the rows go, since the row is where the name comes from.
        ActionMessage = failed.Count switch {
            0 => "",
            1 => $"Couldn't end {NameOf(failed[0])}",
            _ => $"Couldn't end {failed.Count.ToString(CultureInfo.InvariantCulture)} of " +
                 $"{pids.Count.ToString(CultureInfo.InvariantCulture)} processes",
        };

        var stillThere = new HashSet<int>(failed);
        foreach (var pid in pids) {
            if (stillThere.Contains(pid))
                continue;

            _selectedPids.Remove(pid);
            RemoveRow(pid);
        }

        SelectedRow = null;
        ApplySelection();
    }

    /// <summary>Drops a PID's row from whichever group holds it.</summary>
    private void RemoveRow(int pid) {
        foreach (var group in new[] { Apps, Background, WindowsProcesses })
            for (var i = 0; i < group.Count; i++)
                if (group[i].Pid == pid) {
                    group.RemoveAt(i);
                    return;
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
