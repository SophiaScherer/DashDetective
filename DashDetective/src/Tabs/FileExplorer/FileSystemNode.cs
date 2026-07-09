using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// A node in the folder tree. Directory nodes load their children lazily on first expand — each
/// is seeded with a placeholder child so the <c>TreeView</c> renders an expander before we've
/// actually enumerated the folder, matching how Explorer defers the I/O.
/// </summary>
public partial class FileSystemNode : ObservableObject {
    private bool _childrenLoaded;

    public FileSystemNode(string name, string fullPath, bool isDirectory) {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        if (isDirectory)
            Children.Add(LoadingPlaceholder());
    }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }

    public Geometry Glyph => FileTypeCatalog.FolderGlyph;
    public IBrush IconBrush => FileTypeCatalog.FolderBrush;

    public ObservableCollection<FileSystemNode> Children { get; } = new();

    [ObservableProperty] private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value) {
        if (value && !_childrenLoaded) {
            _childrenLoaded = true;
            _ = LoadChildrenAsync();
        }
    }

    private async Task LoadChildrenAsync() {
        IReadOnlyList<DirEntry> subs;
        try {
            subs = await DirectoryService.GetSubdirectoriesAsync(FullPath);
        } catch {
            Children.Clear();
            return;
        }

        // Runs back on the UI thread (the expand toggle originated there), so mutating the
        // bound collection here is safe.
        Children.Clear();
        foreach (var s in subs)
            Children.Add(new FileSystemNode(s.Name, s.FullPath, true));
    }

    // A childless marker row shown until the real children are enumerated on expand.
    private static FileSystemNode LoadingPlaceholder() => new("Loading…", "", false);
}
