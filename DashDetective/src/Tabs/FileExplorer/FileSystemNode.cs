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

    // A childless marker row shown until the real children are enumerated on expand.
    private static FileSystemNode LoadingPlaceholder() =>
        new("Loading…", "", false, false, static () => false, static () => false);
}
