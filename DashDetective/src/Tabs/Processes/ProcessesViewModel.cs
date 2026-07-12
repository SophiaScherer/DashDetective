using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
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

    /// <summary>Foreground apps (own a visible window), updated in place by the keyed diff.</summary>
    public ObservableCollection<ProcessRow> Apps { get; } = new();

    /// <summary>Background processes (services/helpers/Windows components), updated in place.</summary>
    public ObservableCollection<ProcessRow> Background { get; } = new();

    /// <summary>Group header caption for the Apps section (e.g. "Apps · 6").</summary>
    [ObservableProperty] private string _appsHeader = "Apps";

    /// <summary>Group header caption for the Background section (e.g. "Background processes · 214").</summary>
    [ObservableProperty] private string _backgroundHeader = "Background processes";

    public ProcessesViewModel() {
        // Load once immediately so the table isn't blank on arrival, then poll on the timer.
        _ = LoadAsync();

        _timer = new DispatcherTimer { Interval = SampleInterval };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e) => _ = LoadAsync();

    /// <summary>Reads the snapshot off the UI thread and applies it. Guarded against overlap (a slow
    /// enumeration must not pile up ticks) and never throws.</summary>
    private async Task LoadAsync() {
        if (_inFlight)
            return;
        _inFlight = true;
        try {
            var processes = await ProcessSnapshotProvider.GetAsync();
            // Awaited on the UI thread, so the continuation resumes there — safe to touch collections.
            ApplySnapshot(processes);
        } catch {
            Apps.Clear();
            Background.Clear();
            AppsHeader = "Apps";
            BackgroundHeader = "Background processes";
        } finally {
            _inFlight = false;
        }
    }

    /// <summary>Splits the snapshot into the two groups, orders each (by name for now — real column
    /// sorting is a later phase), reconciles them into place and updates the group captions.</summary>
    private void ApplySnapshot(IReadOnlyList<ProcessInfo> processes) {
        var apps = new List<ProcessInfo>();
        var background = new List<ProcessInfo>();
        foreach (var info in processes) {
            if (info.Category == ProcessCategory.App)
                apps.Add(info);
            else
                background.Add(info);
        }

        apps.Sort(CompareByNameThenPid);
        background.Sort(CompareByNameThenPid);

        Reconcile(Apps, apps);
        Reconcile(Background, background);

        AppsHeader = $"Apps · {apps.Count.ToString(CultureInfo.InvariantCulture)}";
        BackgroundHeader = $"Background processes · {background.Count.ToString(CultureInfo.InvariantCulture)}";
    }

    private static int CompareByNameThenPid(ProcessInfo a, ProcessInfo b) {
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
