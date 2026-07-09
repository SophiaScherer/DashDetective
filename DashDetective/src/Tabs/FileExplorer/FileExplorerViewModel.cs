using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Shared;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Read-only three-pane file browser (folder tree · file list · details). Built in phases:
/// this currently drives the folder tree, file list, breadcrumb and filters; the details pane
/// and actions are layered on in later phases.
/// </summary>
public partial class FileExplorerViewModel : ViewModelBase {
    /// <summary>Top-level tree nodes — one per ready drive.</summary>
    public ObservableCollection<FileSystemNode> RootNodes { get; } = new();

    /// <summary>The current folder's entries after the active filter (folders first, then files).</summary>
    public ObservableCollection<FileEntry> VisibleEntries { get; } = new();

    /// <summary>Breadcrumb segments for the current path, root → leaf.</summary>
    public ObservableCollection<Crumb> Crumbs { get; } = new();

    /// <summary>The All / Documents / Images / Archives filter chips.</summary>
    public ObservableCollection<FilterOption> Filters { get; }

    [ObservableProperty] private FileSystemNode? _selectedNode;
    [ObservableProperty] private FileEntry? _selectedEntry;

    /// <summary>Whether the details pane has a file/folder to show.</summary>
    public bool HasSelection => SelectedEntry is not null;
    public bool HasNoSelection => SelectedEntry is null;

    /// <summary>Full path of the currently selected folder (drives the list + breadcrumb).</summary>
    [ObservableProperty] private string _currentPath = "";

    // Full, unfiltered entries of the current folder; VisibleEntries is this through the filter.
    private readonly List<FileEntry> _allEntries = new();
    private FilterOption _selectedFilter;

    // Guards against a slow folder load overwriting the list after the user has moved on.
    private string _pendingPath = "";

    public FileExplorerViewModel() {
        Filters = new ObservableCollection<FilterOption> {
            new FilterOption("All", null, OnFilterSelected),
            new FilterOption("Documents", FileCategory.Document, OnFilterSelected),
            new FilterOption("Images", FileCategory.Image, OnFilterSelected),
            new FilterOption("Archives", FileCategory.Archive, OnFilterSelected),
        };
        _selectedFilter = Filters[0];
        _selectedFilter.IsSelected = true;

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
        SetCurrentFolder(node.FullPath);
    }

    /// <summary>Selects a file-list row (drives the details pane in Phase 4).</summary>
    public void SelectEntry(FileEntry entry) => entry.IsSelected = true;

    /// <summary>Activates a row: folders navigate into themselves. Files open in Phase 5.</summary>
    public void ActivateEntry(FileEntry entry) {
        if (entry.IsDirectory)
            SetCurrentFolder(entry.FullPath);
    }

    private void SetCurrentFolder(string path) {
        CurrentPath = path;
        RebuildCrumbs(path);
        _ = LoadEntriesAsync(path);
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

        SelectedEntry = null;
        _allEntries.Clear();
        foreach (var item in items)
            _allEntries.Add(new FileEntry(item, OnEntrySelected));
        ApplyFilter();
    }

    private void OnEntrySelected(FileEntry entry) {
        // Single selection through our own source of truth, as with the tree.
        if (SelectedEntry is { } prev && !ReferenceEquals(prev, entry))
            prev.IsSelected = false;

        SelectedEntry = entry;
    }

    partial void OnSelectedEntryChanged(FileEntry? value) {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
    }

    private void OnFilterSelected(FilterOption filter) {
        if (ReferenceEquals(filter, _selectedFilter))
            return;

        _selectedFilter.IsSelected = false;
        _selectedFilter = filter;
        filter.IsSelected = true;
        ApplyFilter();
    }

    // Folders always show; files show when the active filter is All or matches their category.
    private void ApplyFilter() {
        VisibleEntries.Clear();
        foreach (var entry in _allEntries) {
            if (_selectedFilter.Category is not { } category
                || entry.IsDirectory
                || FileTypeCatalog.CategoryOf(Path.GetExtension(entry.FullPath)) == category) {
                VisibleEntries.Add(entry);
            }
        }

        // Drop a selection that the filter just hid.
        if (SelectedEntry is { } sel && !VisibleEntries.Contains(sel)) {
            sel.IsSelected = false;
            SelectedEntry = null;
        }
    }

    private void RebuildCrumbs(string path) {
        Crumbs.Clear();
        if (string.IsNullOrEmpty(path))
            return;

        // Climb from the folder to the drive root, then emit root → leaf.
        var chain = new List<DirectoryInfo>();
        for (var dir = new DirectoryInfo(path); dir is not null; dir = dir.Parent)
            chain.Add(dir);
        chain.Reverse();

        for (var i = 0; i < chain.Count; i++) {
            var dir = chain[i];
            var isCurrent = i == chain.Count - 1;
            // The drive root's Name is "C:\"; trim the separator so it reads "C:".
            var label = dir.Parent is null ? dir.Name.TrimEnd(Path.DirectorySeparatorChar) : dir.Name;
            Crumbs.Add(new Crumb(label, dir.FullName, isCurrent ? "" : "›", isCurrent, OnCrumbSelected));
        }
    }

    private void OnCrumbSelected(Crumb crumb) => SetCurrentFolder(crumb.FullPath);
}
