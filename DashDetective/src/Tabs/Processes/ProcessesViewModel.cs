using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;

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
/// flicker. Column sorting, the summary strip, End task and Properties arrive in later phases; this
/// phase orders by name for a stable live list.
/// </summary>
public partial class ProcessesViewModel : ViewModelBase, IRefreshablePage, ILiveSamplingPage, ISelfScrollingPage, IDisposable {
    /// <summary>Poll cadence. Enumerating every process (with per-process window/responding probes) is
    /// heavier than a single counter, so it polls slower than the Dashboard's 1 Hz samplers — close to
    /// Task Manager's own refresh.</summary>
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(2);

    private readonly DispatcherTimer _timer;
    private bool _inFlight;

    // System-wide CPU% / Memory% for the summary strip — the same readings the Dashboard shows, from
    // the shared samplers (promoted to src/Services/SystemMetrics when this tab was activated).
    private readonly CpuUsageSampler _cpuSampler = new();
    private readonly MemoryUsageSampler _memorySampler = new();

    // Sort state: which column + direction. Sorting applies within each group; Apps stay above
    // Background. Defaults to Name ascending (matching the initial list order).
    private ProcessSortKey _sortKey = ProcessSortKey.Name;
    private bool _ascending = true;
    private readonly ProcessSortColumn[] _sortColumns;

    /// <summary>The last snapshot, kept so a header click can re-sort immediately without waiting for
    /// the next poll.</summary>
    private IReadOnlyList<ProcessInfo> _lastSnapshot = Array.Empty<ProcessInfo>();

    /// <summary>The last built process tree (top-level entries with their collapsed children). Kept so
    /// the expand/collapse chevrons can reveal children without rebuilding.</summary>
    private IReadOnlyList<ProcessNode> _lastRoots = Array.Empty<ProcessNode>();

    /// <summary>Foreground apps (own a visible top-level window), updated in place by the keyed diff.
    /// Holds one row per top-level group (a multi-process app collapses to a single entry).</summary>
    public ObservableCollection<ProcessRow> Apps { get; } = new();

    /// <summary>Background processes (user-session helpers/trays/updaters with no window), updated in
    /// place.</summary>
    public ObservableCollection<ProcessRow> Background { get; } = new();

    /// <summary>Windows processes (system/service processes outside the interactive session), updated in
    /// place — Task Manager's third group.</summary>
    public ObservableCollection<ProcessRow> WindowsProcesses { get; } = new();

    // Clickable column headers. Disk/Network/Gpu sort as no-ops until they carry values.
    public ProcessSortColumn NameSort { get; }
    public ProcessSortColumn PidSort { get; }
    public ProcessSortColumn StatusSort { get; }
    public ProcessSortColumn CpuSort { get; }
    public ProcessSortColumn MemorySort { get; }
    public ProcessSortColumn DiskSort { get; }
    public ProcessSortColumn NetworkSort { get; }
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

    public ProcessesViewModel() {
        NameSort = new ProcessSortColumn(ProcessSortKey.Name, OnSort);
        PidSort = new ProcessSortColumn(ProcessSortKey.Pid, OnSort);
        StatusSort = new ProcessSortColumn(ProcessSortKey.Status, OnSort);
        CpuSort = new ProcessSortColumn(ProcessSortKey.Cpu, OnSort);
        MemorySort = new ProcessSortColumn(ProcessSortKey.Memory, OnSort);
        DiskSort = new ProcessSortColumn(ProcessSortKey.Disk, OnSort);
        NetworkSort = new ProcessSortColumn(ProcessSortKey.Network, OnSort);
        GpuSort = new ProcessSortColumn(ProcessSortKey.Gpu, OnSort);
        _sortColumns = new[] {
            NameSort, PidSort, StatusSort, CpuSort, MemorySort, DiskSort, NetworkSort, GpuSort,
        };
        UpdateSortIndicators();

        // Seed the system totals (memory is an absolute reading, so it's real at once; CPU needs an
        // interval, so it reads 0 until the first tick), then load the list and start polling.
        SampleSystemTotals();
        _ = LoadAsync();

        _timer = new DispatcherTimer { Interval = SampleInterval };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e) {
        SampleSystemTotals();
        _ = LoadAsync();
    }

    /// <summary>Reads the system-wide CPU% and Memory% samplers (synchronous, negligible cost) and
    /// updates the summary cards. Never throws.</summary>
    private void SampleSystemTotals() {
        double cpu;
        try { cpu = _cpuSampler.Sample(); } catch { cpu = 0; }
        CpuUsageText = FormatPercent(cpu);

        MemorySample memory;
        try { memory = _memorySampler.Sample(); } catch { memory = default; }
        MemoryUsageText = FormatPercent(memory.LoadPercent);
    }

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

    /// <summary>Builds the process tree (collapsing multi-process apps into one entry, Task-Manager
    /// style), splits the top-level entries into the three groups, orders each by the active sort key,
    /// reconciles them into place and updates the captions. Each row shows its group's aggregate
    /// metrics (own + descendants). Kept for the next poll / a header re-sort.</summary>
    private void ApplySnapshot(IReadOnlyList<ProcessInfo> processes) {
        var roots = ProcessTreeBuilder.Build(processes);
        _lastRoots = roots;

        var apps = new List<ProcessInfo>();
        var background = new List<ProcessInfo>();
        var windows = new List<ProcessInfo>();
        foreach (var node in roots) {
            var entry = node.Aggregate;
            switch (entry.Category) {
                case ProcessCategory.App: apps.Add(entry); break;
                case ProcessCategory.Windows: windows.Add(entry); break;
                default: background.Add(entry); break;
            }
        }

        // Total threads span every process, not just the top-level entries.
        var totalThreads = 0;
        foreach (var info in processes)
            totalThreads += info.ThreadCount;

        apps.Sort(Compare);
        background.Sort(Compare);
        windows.Sort(Compare);

        Reconcile(Apps, apps);
        Reconcile(Background, background);
        Reconcile(WindowsProcesses, windows);

        // If the selected process has exited, the diff removed its row — drop the dangling selection.
        if (SelectedRow is not null && !Apps.Contains(SelectedRow) &&
            !Background.Contains(SelectedRow) && !WindowsProcesses.Contains(SelectedRow)) {
            SelectedRow.IsSelected = false;
            SelectedRow = null;
        }

        AppsHeader = $"Apps · {apps.Count.ToString(CultureInfo.InvariantCulture)}";
        BackgroundHeader = $"Background processes · {background.Count.ToString(CultureInfo.InvariantCulture)}";
        WindowsHeader = $"Windows processes · {windows.Count.ToString(CultureInfo.InvariantCulture)}";

        // Summary strip: the total is the number of top-level entries (so it matches the sum of the
        // three group headers), with the per-group breakdown under it. CPU%/Memory% come from the
        // system samplers in SampleSystemTotals.
        var entries = apps.Count + background.Count + windows.Count;
        TotalProcessesText = entries.ToString(CultureInfo.InvariantCulture);
        ProcessBreakdownText = $"{apps.Count.ToString(CultureInfo.InvariantCulture)} apps · " +
                               $"{background.Count.ToString(CultureInfo.InvariantCulture)} background · " +
                               $"{windows.Count.ToString(CultureInfo.InvariantCulture)} Windows";
        ThreadsText = totalThreads.ToString("N0", CultureInfo.InvariantCulture);
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
        ApplySnapshot(_lastSnapshot);
    }

    /// <summary>Explorer-style defaults: text columns ascending, magnitude columns busiest-first.</summary>
    private static bool DefaultAscending(ProcessSortKey key) => key switch {
        ProcessSortKey.Name => true,
        ProcessSortKey.Pid => true,
        ProcessSortKey.Status => true,
        _ => false, // CPU / Memory / Disk / Network / GPU
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
            _ => 0, // Network is deferred (no data) — fall through to the tie-break.
        };
        if (!_ascending)
            cmp = -cmp;
        if (cmp != 0)
            return cmp;

        var byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        return byName != 0 ? byName : a.Pid.CompareTo(b.Pid);
    }

    /// <summary>Diffs an already-ordered snapshot into <paramref name="target"/> by PID: drops rows no
    /// longer present, updates survivors in place, inserts new ones, and moves survivors to their new
    /// position. Mirrors <c>NetworkViewModel.ReconcileConnections</c>.</summary>
    private static void Reconcile(ObservableCollection<ProcessRow> target, IReadOnlyList<ProcessInfo> incoming) {
        var incomingPids = new HashSet<int>(incoming.Count);
        foreach (var info in incoming)
            incomingPids.Add(info.Pid);

        for (var i = target.Count - 1; i >= 0; i--)
            if (!incomingPids.Contains(target[i].Pid))
                target.RemoveAt(i);

        var existing = new Dictionary<int, ProcessRow>(target.Count);
        foreach (var row in target)
            existing[row.Pid] = row;

        for (var i = 0; i < incoming.Count; i++) {
            var info = incoming[i];
            if (existing.TryGetValue(info.Pid, out var row)) {
                row.Update(info);
                var current = target.IndexOf(row);
                if (current != i)
                    target.Move(current, i);
            } else {
                var created = new ProcessRow(info);
                existing[info.Pid] = created;
                target.Insert(i, created);
            }
        }
    }

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

    /// <summary>Toolbar Refresh: re-sample once immediately. Runs even while paused, like the other tabs.</summary>
    public void Refresh() => OnTick(this, EventArgs.Empty);

    /// <summary>Pauses/resumes the polling timer, driven by the toolbar Live pill. Refresh still works
    /// while paused.</summary>
    public void SetLive(bool live) {
        if (live)
            _timer.Start();
        else
            _timer.Stop();
    }

    /// <summary>Stops the timer. Safe to call more than once.</summary>
    public void Dispose() {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
