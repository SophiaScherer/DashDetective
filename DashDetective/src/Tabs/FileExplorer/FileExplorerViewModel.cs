using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Shared;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Read-only three-pane file browser (folder tree · file list · details). Built in phases:
/// this currently drives the folder tree, file list, breadcrumb and filters; the details pane
/// and actions are layered on in later phases.
/// </summary>
public partial class FileExplorerViewModel : ViewModelBase, ISelfScrollingPage, IRefreshablePage {
    /// <summary>Top-level tree nodes — one per ready drive.</summary>
    public ObservableCollection<FileSystemNode> RootNodes { get; } = new();

    /// <summary>The current folder's entries after the active filter (folders first, then files).</summary>
    public ObservableCollection<FileEntry> VisibleEntries { get; } = new();

    /// <summary>Breadcrumb segments for the current path, root → leaf.</summary>
    public ObservableCollection<Crumb> Crumbs { get; } = new();

    /// <summary>The All / Documents / Images / Archives filter chips.</summary>
    public ObservableCollection<FilterOption> Filters { get; }

    /// <summary>Clickable file-list column headers, bound one-to-one to the header cells.</summary>
    public SortColumn NameSort { get; }
    public SortColumn TypeSort { get; }
    public SortColumn ModifiedSort { get; }
    public SortColumn SizeSort { get; }

    [ObservableProperty] private FileSystemNode? _selectedNode;
    [ObservableProperty] private FileEntry? _selectedEntry;

    /// <summary>Whether the details pane has a file/folder to show.</summary>
    public bool HasSelection => SelectedEntry is not null;
    public bool HasNoSelection => SelectedEntry is null;

    /// <summary>Full path of the currently selected folder (drives the list + breadcrumb).</summary>
    [ObservableProperty] private string _currentPath = "";

    /// <summary>Whether OS hidden/system entries (e.g. AppData) are shown in the list and tree.</summary>
    [ObservableProperty] private bool _showHidden;

    // Full, unfiltered entries of the current folder; VisibleEntries is this through the filter + sort.
    private readonly List<FileEntry> _allEntries = new();
    private FilterOption _selectedFilter;

    // Active sort. Default matches the service baseline (name, ascending); the header columns drive it.
    private readonly SortColumn[] _sortColumns;
    private FileSortKey _sortKey = FileSortKey.Name;
    private bool _sortDescending;

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

        NameSort = new SortColumn(FileSortKey.Name, OnSort);
        TypeSort = new SortColumn(FileSortKey.Type, OnSort);
        ModifiedSort = new SortColumn(FileSortKey.Modified, OnSort);
        SizeSort = new SortColumn(FileSortKey.Size, OnSort);
        _sortColumns = new[] { NameSort, TypeSort, ModifiedSort, SizeSort };
        UpdateSortIndicators();

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
            RootNodes.Add(new FileSystemNode(d.DisplayName, d.RootPath, true, d.HasChildren, () => ShowHidden, OnNodeSelected));
    }

    // Toggling "show hidden" reloads the file list and rebuilds the tree from its roots (whose lazy
    // nodes capture ShowHidden, so re-reading re-applies the setting). Expanded folders collapse —
    // an acceptable trade for a rarely-flipped toggle.
    partial void OnShowHiddenChanged(bool value) {
        _ = LoadRootsAsync();
        if (!string.IsNullOrEmpty(CurrentPath))
            _ = LoadEntriesAsync(CurrentPath);
    }

    /// <summary>Toolbar Refresh for the File Explorer: re-read the current folder (picking up files
    /// added/removed on disk), or reload the drive roots if nothing is open yet. Reuses the same
    /// load path as navigation, so the stale-load guard still applies.</summary>
    public void Refresh() {
        if (!string.IsNullOrEmpty(CurrentPath))
            SetCurrentFolder(CurrentPath);
        else
            _ = LoadRootsAsync();
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

    /// <summary>Activates a row: folders navigate into themselves; files open in their default app.</summary>
    public void ActivateEntry(FileEntry entry) {
        if (entry.IsDirectory)
            SetCurrentFolder(entry.FullPath);
        else
            ShellInterop.Open(entry.FullPath);
    }

    /// <summary>Opens the selected entry (details-pane Open button).</summary>
    [RelayCommand]
    private void Open() {
        if (SelectedEntry is { } entry)
            ShellInterop.Open(entry.FullPath);
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
            items = await DirectoryService.GetEntriesAsync(path, ShowHidden);
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
        RebuildVisibleEntries();
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
        RebuildVisibleEntries();
    }

    // Clicking a header re-sorts: the same column flips direction, a new column adopts its
    // Explorer-style default (text columns ascending, Modified/Size descending — newest/largest first).
    private void OnSort(FileSortKey key) {
        if (key == _sortKey) {
            _sortDescending = !_sortDescending;
        } else {
            _sortKey = key;
            _sortDescending = key is FileSortKey.Modified or FileSortKey.Size;
        }
        UpdateSortIndicators();
        RebuildVisibleEntries();
    }

    // Tint + arrow follow the active column and direction.
    private void UpdateSortIndicators() {
        foreach (var col in _sortColumns) {
            col.IsActive = col.Key == _sortKey;
            col.Arrow = col.IsActive ? (_sortDescending ? "↓" : "↑") : "";
        }
    }

    // Folders always show; files show when the active filter is All or matches their category.
    // The filtered set is then ordered by the active column.
    private void RebuildVisibleEntries() {
        var filtered = new List<FileEntry>(_allEntries.Count);
        foreach (var entry in _allEntries) {
            if (_selectedFilter.Category is not { } category
                || entry.IsDirectory
                || FileTypeCatalog.CategoryOf(Path.GetExtension(entry.FullPath)) == category) {
                filtered.Add(entry);
            }
        }
        filtered.Sort(Compare);

        VisibleEntries.Clear();
        foreach (var entry in filtered)
            VisibleEntries.Add(entry);

        // Drop a selection that the filter just hid.
        if (SelectedEntry is { } sel && !VisibleEntries.Contains(sel)) {
            sel.IsSelected = false;
            SelectedEntry = null;
        }
    }

    // Folders always precede files (the grouping is never inverted by direction); within a group,
    // order by the active column, breaking ties by name, then apply the descending flag.
    private int Compare(FileEntry a, FileEntry b) {
        if (a.IsDirectory != b.IsDirectory)
            return a.IsDirectory ? -1 : 1;

        var cmp = _sortKey switch {
            FileSortKey.Type => string.Compare(a.TypeName, b.TypeName, StringComparison.OrdinalIgnoreCase),
            FileSortKey.Modified => a.Modified.CompareTo(b.Modified),
            FileSortKey.Size => a.Size.CompareTo(b.Size),
            _ => 0,
        };
        if (cmp == 0)
            cmp = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

        return _sortDescending ? -cmp : cmp;
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
