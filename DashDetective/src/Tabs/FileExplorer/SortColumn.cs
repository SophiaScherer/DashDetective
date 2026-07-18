using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// A clickable file-list column header. Clicking sorts by its <see cref="Key"/>; the active column
/// shows a direction <see cref="Arrow"/> and is tinted (via <see cref="IsActive"/>). Same
/// selectable-item-VM shape as <see cref="FilterOption"/>.
/// </summary>
public partial class SortColumn : ObservableObject {
    public SortColumn(FileSortKey key, Action<FileSortKey> onSort) {
        Key = key;
        SortCommand = new RelayCommand(() => onSort(key));
    }

    public FileSortKey Key { get; }
    public ICommand SortCommand { get; }

    /// <summary>True when the list is currently sorted by this column.</summary>
    [ObservableProperty] private bool _isActive;

    /// <summary>"↑" / "↓" when active, empty otherwise.</summary>
    [ObservableProperty] private string _arrow = "";
}
