using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// A single row in the centre file list. Built from a <see cref="FileItem"/> (whose display
/// strings are computed off the UI thread) plus the themed glyph/colour. Selection is reported
/// through an <c>onSelected</c> callback when <see cref="IsSelected"/> flips true — the same
/// pattern as <see cref="FileSystemNode"/> and NavItem.
/// </summary>
public partial class FileEntry : ObservableObject {
    private readonly Action<FileEntry>? _onSelected;

    public FileEntry(FileItem item, Action<FileEntry>? onSelected = null) {
        Name = item.Name;
        FullPath = item.FullPath;
        IsDirectory = item.IsDirectory;
        TypeName = item.TypeName;
        ModifiedText = item.ModifiedText;
        SizeText = item.SizeText;
        CreatedText = item.CreatedText;
        AttributesText = item.AttributesText;
        Location = Path.GetDirectoryName(item.FullPath) ?? item.FullPath;
        Size = item.Size;
        Modified = item.Modified;
        (Glyph, IconBrush) = FileTypeCatalog.ForEntry(item.IsDirectory, item.Extension);
        _onSelected = onSelected;
    }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public string TypeName { get; }
    public string ModifiedText { get; }
    public string SizeText { get; }
    public string CreatedText { get; }
    public string AttributesText { get; }
    public string Location { get; }

    // Raw sort keys (the display strings above can't be ordered). Size is bytes, -1 for folders.
    public long Size { get; }
    public DateTime Modified { get; }

    public Geometry Glyph { get; }
    public IBrush IconBrush { get; }

    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) {
        if (value)
            _onSelected?.Invoke(this);
    }
}
