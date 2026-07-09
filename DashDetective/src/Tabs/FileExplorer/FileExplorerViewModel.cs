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

    [ObservableProperty] private FileSystemNode? _selectedNode;

    /// <summary>Full path of the currently selected folder (drives the list/breadcrumb later).</summary>
    [ObservableProperty] private string _currentPath = "";

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
        // Phase 3 wires the file list + breadcrumb off the selected folder.
    }
}
