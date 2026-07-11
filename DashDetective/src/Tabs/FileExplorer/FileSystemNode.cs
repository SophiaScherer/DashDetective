using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// A node in the folder tree. Directory nodes load their children lazily on first expand — a node
/// known to have subfolders (<paramref name="hasChildren"/>) is seeded with a placeholder child so
/// the <c>TreeView</c> renders an expander before we've actually enumerated the folder, matching how
/// Explorer defers the I/O; an empty folder gets no placeholder and so shows no chevron. Selection is
/// reported through an <c>onSelected</c> callback when <see cref="IsSelected"/> flips true (the
/// NavItem pattern), so the owning view model can react without depending on the control's selection API.
/// </summary>
public partial class FileSystemNode : ObservableObject {
    private readonly Action<FileSystemNode>? _onSelected;
    // Read live on each lazy expand so the tree honors the current "show hidden" toggle.
    private readonly Func<bool> _includeHidden;
    // Read live on each collapse: when true, collapsing this node also collapses its descendants.
    private readonly Func<bool> _collapseChildren;
    private bool _childrenLoaded;

    public FileSystemNode(string name, string fullPath, bool isDirectory, bool hasChildren,
                          Func<bool> includeHidden, Func<bool> collapseChildren,
                          Action<FileSystemNode>? onSelected = null) {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        _includeHidden = includeHidden;
        _collapseChildren = collapseChildren;
        _onSelected = onSelected;
        // Seed the expander placeholder only for folders that actually have a subfolder, so empty
        // folders render without a chevron.
        if (isDirectory && hasChildren)
            Children.Add(LoadingPlaceholder());
    }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }

    public Geometry Glyph => FileTypeCatalog.FolderGlyph;
    public IBrush IconBrush => FileTypeCatalog.FolderBrush;

    public ObservableCollection<FileSystemNode> Children { get; } = new();

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) {
        if (value)
            _onSelected?.Invoke(this);
    }

    partial void OnIsExpandedChanged(bool value) {
        if (value && !_childrenLoaded) {
            _childrenLoaded = true;
            _ = LoadChildrenAsync();
        } else if (!value && _collapseChildren()) {
            // Collapsing with the option on resets the branch: collapse each child, whose own
            // setter re-enters here and cascades. Already-collapsed nodes are no-ops, so the cost
            // is bounded to the currently-expanded subtree.
            foreach (var child in Children)
                child.IsExpanded = false;
        }
    }

    /// <summary>
    /// Re-reads this node's subfolders against the <em>current</em> hidden setting and merges them into
    /// <see cref="Children"/> in place: surviving folders keep their instance (and so their expansion,
    /// selection and any loaded subtree), newly-visible folders are inserted, and vanished ones removed.
    /// An unexpanded node stays lazy — its subtree isn't loaded — but its expander is kept honest, so a
    /// folder that just gained (or lost) its first subfolder shows (or hides) its chevron. Recurses into
    /// survivors that are themselves loaded. Used by the "show hidden" toggle and the auto-refresh
    /// watcher to update the tree without collapsing it.
    /// </summary>
    public async Task SyncChildrenAsync() {
        IReadOnlyList<DirEntry> subs;
        try {
            subs = await DirectoryService.GetSubdirectoriesAsync(FullPath, _includeHidden());
        } catch {
            return;
        }

        // Not yet expanded: don't load the subtree (keep it lazy), but seed or clear the "Loading…"
        // placeholder so the chevron reflects whether the folder now has subfolders. The real children
        // are read on the next lazy expand.
        if (!_childrenLoaded) {
            SetExpanderVisible(subs.Count > 0);
            return;
        }

        // Existing children and the freshly-read list are both sorted by name, and survivors keep their
        // relative order, so one ordered pass aligns them: drop what's gone, then insert what's new at
        // its sorted position (a stale "Loading…" placeholder has an empty path and so is dropped too).
        var present = new HashSet<string>(subs.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var s in subs)
            present.Add(s.FullPath);

        for (var i = Children.Count - 1; i >= 0; i--)
            if (!present.Contains(Children[i].FullPath))
                Children.RemoveAt(i);

        for (var i = 0; i < subs.Count; i++) {
            var s = subs[i];
            if (i < Children.Count &&
                string.Equals(Children[i].FullPath, s.FullPath, StringComparison.OrdinalIgnoreCase))
                continue;
            Children.Insert(i, new FileSystemNode(s.Name, s.FullPath, true, s.HasChildren,
                                                  _includeHidden, _collapseChildren, _onSelected));
        }

        // Same-class private access lets a parent recurse into a loaded survivor's own subtree.
        foreach (var child in Children)
            if (child._childrenLoaded)
                await child.SyncChildrenAsync();
    }

    private async Task LoadChildrenAsync() {
        IReadOnlyList<DirEntry> subs;
        try {
            subs = await DirectoryService.GetSubdirectoriesAsync(FullPath, _includeHidden());
        } catch {
            Children.Clear();
            return;
        }

        // Runs back on the UI thread (the expand toggle originated there), so mutating the
        // bound collection here is safe. Children inherit the same hidden accessor + selection callback.
        Children.Clear();
        foreach (var s in subs)
            Children.Add(new FileSystemNode(s.Name, s.FullPath, true, s.HasChildren,
                                            _includeHidden, _collapseChildren, _onSelected));
    }

    // Adds or removes the placeholder that drives the chevron on an unexpanded node, so an empty folder
    // that just gained its first subfolder (or lost its last) toggles its expander. Only ever touches
    // the placeholder — a real, loaded subtree goes through the merge path in SyncChildrenAsync instead.
    private void SetExpanderVisible(bool visible) {
        if (visible && Children.Count == 0)
            Children.Add(LoadingPlaceholder());
        else if (!visible && Children.Count == 1 && string.IsNullOrEmpty(Children[0].FullPath))
            Children.Clear();
    }

    // A childless marker row shown until the real children are enumerated on expand.
    private static FileSystemNode LoadingPlaceholder() =>
        new("Loading…", "", false, false, static () => false, static () => false);
}
