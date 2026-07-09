using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Shared;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Read-only three-pane file browser (folder tree · file list · details). Built in phases:
/// this currently drives the folder tree; the file list, breadcrumb, details and actions
/// are layered on in later phases.
/// </summary>
public partial class FileExplorerViewModel : ViewModelBase {
    /// <summary>Top-level tree nodes — one per ready drive.</summary>
    public ObservableCollection<FileSystemNode> RootNodes { get; } = new();

    /// <summary>Entries of the currently selected folder (folders first, then files).</summary>
    public ObservableCollection<FileEntry> Entries { get; } = new();

    [ObservableProperty] private FileSystemNode? _selectedNode;
    [ObservableProperty] private FileEntry? _selectedEntry;

    /// <summary>Full path of the currently selected folder (drives the list/breadcrumb later).</summary>
    [ObservableProperty] private string _currentPath = "";

    // Guards against a slow folder load overwriting the list after the user has moved on.
    private string _pendingPath = "";

    public FileExplorerViewModel() {
        // Load drives off the UI thread; the continuation resumes here (UI thread) to fill
        // the bound collection. Mirrors the Dashboard providers' fire-and-forget load.
        _ = LoadRootsAsync();
    }

    private async Task LoadRootsAsync() {
        IReadOnlyList<DriveEntry> drives;
        try {
            drives = await DirectoryService.GetDrivesAsync();
        } catch {
            return;
        }

        RootNodes.Clear();
        foreach (var d in drives)
            RootNodes.Add(new FileSystemNode(d.DisplayName, d.RootPath, true, OnNodeSelected));
    }

    private void OnNodeSelected(FileSystemNode node) {
        // Enforce single selection through our own source of truth (the NavItem pattern):
        // two-way IsSelected binding alone doesn't reliably clear the previously selected
        // node, which otherwise leaves every visited ancestor highlighted.
        if (SelectedNode is { } prev && !ReferenceEquals(prev, node))
            prev.IsSelected = false;

        SelectedNode = node;
        CurrentPath = node.FullPath;
        _ = LoadEntriesAsync(node.FullPath);
    }

    private async Task LoadEntriesAsync(string path) {
        _pendingPath = path;
        IReadOnlyList<FileItem> items;
        try {
            items = await DirectoryService.GetEntriesAsync(path);
        } catch {
            return;
        }

        // Ignore a stale load if the user has since selected another folder.
        if (_pendingPath != path)
            return;

        Entries.Clear();
        SelectedEntry = null;
        foreach (var item in items)
            Entries.Add(new FileEntry(item, OnEntrySelected));
    }

    private void OnEntrySelected(FileEntry entry) {
        // Single selection through our own source of truth, as with the tree.
        if (SelectedEntry is { } prev && !ReferenceEquals(prev, entry))
            prev.IsSelected = false;

        SelectedEntry = entry;
        // Phase 3b: double-clicking a folder navigates into it. Phase 4: build details.
    }
}
