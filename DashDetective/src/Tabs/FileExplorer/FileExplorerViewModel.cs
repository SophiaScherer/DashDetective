using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Read-only three-pane file browser (folder tree · file list · details). Built in phases:
/// this currently drives the folder tree, file list, breadcrumb and filters; the details pane
/// and actions are layered on in later phases.
/// </summary>
public partial class FileExplorerViewModel : ViewModelBase, ISelfScrollingPage, IRefreshablePage, IShortcutTarget, IDisposable {
    /// <summary>Top-level tree nodes — one per ready drive.</summary>
    public ObservableCollection<FileSystemNode> RootNodes { get; } = new();

    /// <summary>The current folder's entries after the active filter (folders first, then files).</summary>
    public ObservableCollection<FileEntry> VisibleEntries { get; } = new();

    /// <summary>Breadcrumb segments for the current path, root → leaf.</summary>
    public ObservableCollection<Crumb> Crumbs { get; } = new();

    /// <summary>The All / Documents / Images / Archives filter chips.</summary>
    public ObservableCollection<FilterOption> Filters { get; }

    /// <summary>Raised when the user navigates to a different folder (not on a same-folder reload
    /// from sort/filter/Refresh), so the view can reset the file list back to the top.</summary>
    public event Action? ScrollToTopRequested;

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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoUp))]
    private string _currentPath = "";

    // ----- Responsive table columns -----

    // Starts unconstrained so the list shows every column on the first layout pass, before the view
    // has reported a width.
    private double _tableWidth = double.PositiveInfinity;
    private int _visibleColumns = FileExplorerTableLayout.Minimums.Length;

    /// <summary>The file list's ColumnDefinitions at the current width. The column header and the row
    /// template both bind to this, so they cannot fall out of alignment.</summary>
    public string ColumnLayout => FileExplorerTableLayout.Definitions(_tableWidth);

    public bool ShowTypeColumn => FileExplorerTableLayout.ShowType(_tableWidth);

    public bool ShowModifiedColumn => FileExplorerTableLayout.ShowModified(_tableWidth);

    /// <summary>Reports the width the list is laid out in. This pane sits between two splitters, so it
    /// narrows both with the window and when the user drags a pane — the same rule covers both. Only
    /// re-notifies when the column set actually changes, so a drag doesn't churn bindings.</summary>
    public void SetTableWidth(double width) {
        if (!double.IsFinite(width) || width <= 0)
            return;

        _tableWidth = width;
        var visible = FileExplorerTableLayout.VisibleCount(width);
        if (visible == _visibleColumns)
            return;

        _visibleColumns = visible;
        OnPropertyChanged(nameof(ColumnLayout));
        OnPropertyChanged(nameof(ShowTypeColumn));
        OnPropertyChanged(nameof(ShowModifiedColumn));
    }

    // ----- Navigation history -----

    private readonly NavigationHistory _history = new();

    /// <summary>Whether Back has somewhere to return to.</summary>
    public bool CanGoBack => _history.CanGoBack;

    /// <summary>Whether Forward has a trail left to retrace.</summary>
    public bool CanGoForward => _history.CanGoForward;

    /// <summary>Whether the current folder has a parent to climb to (false at a drive root).</summary>
    public bool CanGoUp => ParentOfCurrent() is not null;

    // ----- Address bar -----

    /// <summary>Whether the breadcrumb has been swapped for an editable path box (Ctrl+L).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCrumbs))]
    private bool _isPathEditing;

    /// <summary>The path being typed. Only meaningful while <see cref="IsPathEditing"/>.</summary>
    [ObservableProperty] private string _pathText = "";

    /// <summary>The folder path the address bar should complete to, ghosted after the caret for Tab to
    /// accept — type <c>C:\Us</c> and <c>ers</c> appears, as Windows Explorer's own bar does.</summary>
    [ObservableProperty] private string? _pathCompletionText;

    // Reads the typed folder's children to complete the last segment, caching them so a name typed a
    // character at a time doesn't re-enumerate the same folder once per keystroke.
    private readonly PathCompletion _pathCompletion = new();

    partial void OnPathTextChanged(string value) => _ = UpdatePathCompletionAsync(value);

    private async Task UpdatePathCompletionAsync(string typed) {
        var completion = await _pathCompletion.CompleteAsync(typed, ShowHidden);

        // The read is asynchronous, so the user may have typed on since; a suggestion for an older
        // prefix would ghost in the wrong text.
        if (PathText == typed)
            PathCompletionText = completion;
    }

    /// <summary>Whether the breadcrumb trail is showing (it and the path box swap places).</summary>
    public bool ShowCrumbs => !IsPathEditing;

    /// <summary>Raised once the path box is showing, so the view can put the caret in it. UI-only.</summary>
    public event Action? PathEditRequested;

    /// <summary>Swaps the breadcrumb for the path box, seeded with the current folder.</summary>
    [RelayCommand]
    private void BeginPathEdit() {
        PathText = CurrentPath;
        IsPathEditing = true;
        PathEditRequested?.Invoke();
    }

    /// <summary>Leaves the path box without navigating (Esc, or clicking away).</summary>
    [RelayCommand]
    private void CancelPathEdit() => IsPathEditing = false;

    /// <summary>Opens the typed folder. A path that doesn't name a reachable folder simply reverts to
    /// the breadcrumb — a typo shouldn't throw at a keystroke, matching how the rest of the page
    /// soft-fails on file-system errors.</summary>
    [RelayCommand]
    private void CommitPath() {
        var path = PathText.Trim().Trim('"');
        IsPathEditing = false;

        if (path.Length == 0)
            return;

        try {
            if (Directory.Exists(path))
                SetCurrentFolder(Path.GetFullPath(path));
        } catch {
            // Malformed path (bad characters, too long, no permission to resolve) — stay put.
        }
    }

    /// <summary>Whether OS hidden/system entries (e.g. AppData) are shown in the list and tree.</summary>
    [ObservableProperty] private bool _showHidden;

    /// <summary>When on, collapsing a tree node also collapses all of its descendants, so
    /// re-expanding it shows a clean single level instead of the branch's prior expansion state.
    /// Read live by each node on collapse; no reload needed since only future gestures are affected.</summary>
    [ObservableProperty] private bool _collapseChildrenWithParent;

    // Full, unfiltered entries of the current folder; VisibleEntries is this through the filter + sort.
    private readonly List<FileEntry> _allEntries = new();
    private FilterOption _selectedFilter;

    // Active sort. Default matches the service baseline (name, ascending); the header columns drive it.
    private readonly SortColumn[] _sortColumns;
    private FileSortKey _sortKey = FileSortKey.Name;
    private bool _sortDescending;

    // Guards against a slow folder load overwriting the list after the user has moved on.
    private string _pendingPath = "";

    // Auto-refresh: one watcher, re-pointed at the open folder on each navigation, raises a debounced
    // event when items are added/removed on disk. The page is a long-lived singleton that's never
    // disposed, so the watcher simply lives for the app's lifetime — no teardown plumbing needed.
    private readonly DirectoryWatcher _watcher = new();

    // When set, the next folder load re-selects this path if it still exists (auto-refresh preserves
    // the user's selection; navigation leaves it null so selection clears as before).
    private string? _reselectPath;

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

        // Fires on a timer thread — hop to the UI thread before touching bound collections.
        _watcher.Changed += () => Dispatcher.UIThread.Post(ReloadCurrentFolderPreservingState);

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
            RootNodes.Add(new FileSystemNode(d.DisplayName, d.RootPath, true, d.HasChildren,
                                             () => ShowHidden, () => CollapseChildrenWithParent, OnNodeSelected));
    }

    // Toggling "show hidden" reconciles each loaded tree branch in place (adding/removing hidden
    // folders while keeping expansion and selection) and reloads the file list so its hidden files
    // appear or disappear. The drive roots themselves never change, so they're not rebuilt.
    partial void OnShowHiddenChanged(bool value) {
        foreach (var node in RootNodes)
            _ = node.SyncChildrenAsync();
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

    /// <summary>
    /// Opens the page at a path, for a jump from universal search: a folder is navigated into, a file
    /// has its folder opened with the file selected.
    ///
    /// Selection rides the same <c>_reselectPath</c> the auto-refresh uses to keep a selection across a
    /// reload — the folder load is asynchronous either way, so there is nothing to select until it
    /// lands. The category chips are reset first: arriving at the folder with the file you searched for
    /// filtered out of the list would be the one outcome worse than not jumping at all.
    /// </summary>
    public void Reveal(string fullPath) {
        if (string.IsNullOrWhiteSpace(fullPath))
            return;

        try {
            OnFilterSelected(Filters[0]);

            if (Directory.Exists(fullPath)) {
                SetCurrentFolder(Path.GetFullPath(fullPath));
                return;
            }

            if (Path.GetDirectoryName(fullPath) is not { } folder || !Directory.Exists(folder))
                return;

            _reselectPath = fullPath;
            SetCurrentFolder(Path.GetFullPath(folder));
        } catch {
            // Malformed path, or one that vanished between the search and the jump — stay put, the same
            // way a typo in the path box does.
        }
    }

    /// <summary>Opens a folder. <paramref name="recordHistory"/> is false only when the move *is* a
    /// history step (Back/Forward), which must move between the stacks rather than push onto them.</summary>
    private void SetCurrentFolder(string path, bool recordHistory = true) {
        // Navigating to a *different* folder resets the list scroll to the top; a same-path reload
        // (sort, filter, Refresh, auto-refresh) leaves the user where they were.
        var isNavigation = !string.Equals(path, CurrentPath, StringComparison.OrdinalIgnoreCase);

        if (isNavigation && recordHistory)
            _history.Record(CurrentPath);

        CurrentPath = path;
        RebuildCrumbs(path);
        _watcher.Watch(path);
        _ = LoadEntriesAsync(path);

        if (isNavigation) {
            SyncTreeSelection(path);
            ScrollToTopRequested?.Invoke();
        }

        NotifyNavigationState();
    }

    /// <summary>Goes back to the previously open folder.</summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack() {
        if (_history.TryGoBack(CurrentPath, out var target))
            SetCurrentFolder(target, recordHistory: false);
    }

    /// <summary>Retraces a step undone by Back.</summary>
    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward() {
        if (_history.TryGoForward(CurrentPath, out var target))
            SetCurrentFolder(target, recordHistory: false);
    }

    /// <summary>Climbs to the parent folder. Going up is an ordinary navigation, so Back returns to the
    /// folder you climbed out of.</summary>
    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private void GoUp() {
        if (ParentOfCurrent() is { } parent)
            SetCurrentFolder(parent);
    }

    /// <summary>The current folder's parent, or null at a drive root or before anything is open.
    /// Soft-fails to null on a malformed path rather than throwing at a keystroke.</summary>
    private string? ParentOfCurrent() {
        if (string.IsNullOrEmpty(CurrentPath))
            return null;

        try {
            return Directory.GetParent(CurrentPath)?.FullName;
        } catch {
            return null;
        }
    }

    /// <summary>Re-evaluates what the navigation buttons can do after a move.</summary>
    private void NotifyNavigationState() {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
        GoUpCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Keeps the folder tree's highlight honest when the move didn't come from the tree (Back,
    /// Up, a breadcrumb, or opening a folder from the list): selects the matching node when that branch
    /// is already loaded, and otherwise clears the old highlight rather than leaving it pointing at a
    /// folder the user is no longer in.</summary>
    private void SyncTreeSelection(string path) {
        var node = FindNode(RootNodes, path);
        if (ReferenceEquals(node, SelectedNode))
            return;

        if (SelectedNode is { } previous)
            previous.IsSelected = false;

        SelectedNode = node;
        if (node is not null)
            node.IsSelected = true;
    }

    // Auto-refresh: the open folder changed on disk. Reload its list (keeping the current selection by
    // path if it survived) and reconcile the matching tree branch so new/removed subfolders show there
    // too. It's a same-path reload, so SetCurrentFolder isn't involved and the scroll position is kept.
    private void ReloadCurrentFolderPreservingState() {
        if (string.IsNullOrEmpty(CurrentPath))
            return;

        _reselectPath = SelectedEntry?.FullPath;
        _ = LoadEntriesAsync(CurrentPath);

        if (FindNode(RootNodes, CurrentPath) is { } node)
            _ = node.SyncChildrenAsync();
    }

    // Depth-first search for the tree node at a path; used to point tree updates at the open folder.
    private static FileSystemNode? FindNode(IEnumerable<FileSystemNode> nodes, string path) {
        foreach (var node in nodes) {
            if (string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase))
                return node;
            if (FindNode(node.Children, path) is { } found)
                return found;
        }
        return null;
    }

    private async Task LoadEntriesAsync(string path) {
        _pendingPath = path;
        // Consume the reselect request up front so only this load restores it (a navigation load,
        // which leaves it null, still clears the selection below).
        var reselect = _reselectPath;
        _reselectPath = null;

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

        // Auto-refresh keeps the user's selection: re-select the same path if it survived the change.
        if (reselect is not null)
            foreach (var entry in VisibleEntries)
                if (string.Equals(entry.FullPath, reselect, StringComparison.OrdinalIgnoreCase)) {
                    entry.IsSelected = true;
                    break;
                }
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

    /// <summary>
    /// The page's keyboard shortcuts. Each returns false when it has nothing to act on — at a drive root
    /// there is nowhere to go up to, and with no row selected there is nothing to open — so the key
    /// falls through to the shell instead of being silently swallowed.
    /// </summary>
    public ShortcutScope Scope => ShortcutScope.FileExplorer;

    public bool HandleShortcut(ShortcutId id) {
        // The path box is modal for this page: while it's open Esc backs out of it, and the navigation
        // keys stay out of the way of the text being typed.
        if (IsPathEditing) {
            if (id != ShortcutId.Escape)
                return false;

            CancelPathEdit();
            return true;
        }

        switch (id) {
            case ShortcutId.FocusAddressBar:
                BeginPathEdit();
                return true;

            case ShortcutId.NavigateBack when CanGoBack:
                GoBack();
                return true;

            case ShortcutId.NavigateForward when CanGoForward:
                GoForward();
                return true;

            case ShortcutId.NavigateUp when CanGoUp:
                GoUp();
                return true;

            case ShortcutId.Activate when SelectedEntry is { } entry:
                ActivateEntry(entry);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Disposes the directory watcher (its <see cref="FileSystemWatcher"/> and debounce timer)
    /// on shutdown. Safe to call more than once.</summary>
    public void Dispose() => _watcher.Dispose();
}
