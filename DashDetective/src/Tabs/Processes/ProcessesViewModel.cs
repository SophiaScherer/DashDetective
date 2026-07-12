using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
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

    /// <summary>Foreground apps (own a visible window), updated in place by the keyed diff.</summary>
    public ObservableCollection<ProcessRow> Apps { get; } = new();

    /// <summary>Background processes (services/helpers/Windows components), updated in place.</summary>
    public ObservableCollection<ProcessRow> Background { get; } = new();

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

    /// <summary>Group header caption for the Background section (e.g. "Background processes · 214").</summary>
    [ObservableProperty] private string _backgroundHeader = "Background processes";

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
            Apps.Clear();
            Background.Clear();
            AppsHeader = "Apps";
            BackgroundHeader = "Background processes";
            TotalProcessesText = "0";
            ProcessBreakdownText = "";
            ThreadsText = "0";
        } finally {
            _inFlight = false;
        }
    }

    /// <summary>Splits the snapshot into the two groups, orders each (by name for now — real column
    /// sorting is a later phase), reconciles them into place and updates the group captions.</summary>
    private void ApplySnapshot(IReadOnlyList<ProcessInfo> processes) {
        var apps = new List<ProcessInfo>();
        var background = new List<ProcessInfo>();
        var totalThreads = 0;
        foreach (var info in processes) {
            if (info.Category == ProcessCategory.App)
                apps.Add(info);
            else
                background.Add(info);
            totalThreads += info.ThreadCount;
        }

        apps.Sort(Compare);
        background.Sort(Compare);

        Reconcile(Apps, apps);
        Reconcile(Background, background);

        AppsHeader = $"Apps · {apps.Count.ToString(CultureInfo.InvariantCulture)}";
        BackgroundHeader = $"Background processes · {background.Count.ToString(CultureInfo.InvariantCulture)}";

        // Summary strip: total count + per-group breakdown + total threads (CPU%/Memory% come from
        // the system samplers in SampleSystemTotals).
        TotalProcessesText = (apps.Count + background.Count).ToString(CultureInfo.InvariantCulture);
        ProcessBreakdownText = $"{apps.Count.ToString(CultureInfo.InvariantCulture)} apps · " +
                               $"{background.Count.ToString(CultureInfo.InvariantCulture)} background";
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
            _ => 0, // Disk / Network / GPU carry no data yet — fall through to the tie-break.
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
