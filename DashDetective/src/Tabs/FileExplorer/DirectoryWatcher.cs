using System;
using System.IO;
using System.Threading;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Watches a single directory (non-recursively) for file/folder changes and raises a debounced
/// <see cref="Changed"/> event, so the File Explorer can auto-refresh the open folder as the user
/// adds or removes items on disk. Mirrors <see cref="DirectoryService"/>'s defensive, Windows-guarded
/// style: a vanished or denied path leaves the watcher idle instead of throwing. The event fires on a
/// timer thread — subscribers marshal to the UI thread themselves, keeping this UI-framework-agnostic.
/// </summary>
public sealed class DirectoryWatcher : IDisposable {
    // The OS raises a burst of events for a single user action (a create or rename can fire several),
    // so we coalesce them into one refresh once changes have been quiet for this long.
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(300);

    private readonly FileSystemWatcher? _watcher;
    private readonly Timer _debounce;
    private bool _disposed;

    /// <summary>Raised once per burst of changes in the watched folder (on a timer thread).</summary>
    public event Action? Changed;

    public DirectoryWatcher() {
        _debounce = new Timer(_ => Changed?.Invoke());
        if (!OperatingSystem.IsWindows())
            return;

        _watcher = new FileSystemWatcher {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                         | NotifyFilters.Size | NotifyFilters.LastWrite,
        };
        _watcher.Created += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Renamed += OnFileSystemEvent;
        _watcher.Changed += OnFileSystemEvent;
        // A buffer overflow means we missed events — schedule a resync so the list can't drift.
        _watcher.Error += (_, _) => ScheduleRaise();
    }

    /// <summary>Points the watcher at <paramref name="path"/> and starts raising events. A missing or
    /// inaccessible path simply leaves the watcher idle (no throw).</summary>
    public void Watch(string path) {
        if (_watcher is null || _disposed)
            return;
        try {
            _watcher.EnableRaisingEvents = false;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;
            _watcher.Path = path;
            _watcher.EnableRaisingEvents = true;
        } catch {
            // Path became inaccessible between the check and the set; stay idle.
            try { _watcher.EnableRaisingEvents = false; } catch { /* already down */ }
        }
    }

    private void OnFileSystemEvent(object? sender, FileSystemEventArgs e) => ScheduleRaise();

    // Reset the debounce timer so Changed fires once, DebounceWindow after the last event in a burst.
    private void ScheduleRaise() {
        if (_disposed)
            return;
        try { _debounce.Change(DebounceWindow, Timeout.InfiniteTimeSpan); } catch { /* disposed */ }
    }

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;
        _debounce.Dispose();
        _watcher?.Dispose();
    }
}
