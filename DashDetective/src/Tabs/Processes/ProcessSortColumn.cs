using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// A clickable process-table column header. Clicking sorts by its <see cref="Key"/>; the active column
/// shows a direction <see cref="Arrow"/> and is tinted (via <see cref="IsActive"/>). Same
/// selectable-item-VM shape as File Explorer's <c>SortColumn</c>, kept tab-local (promote a shared
/// <c>SortColumn&lt;TKey&gt;</c> to src/Shared once a cross-tab refactor is signed off).
/// </summary>
public partial class ProcessSortColumn : ObservableObject {
    public ProcessSortColumn(ProcessSortKey key, Action<ProcessSortKey> onSort) {
        Key = key;
        SortCommand = new RelayCommand(() => onSort(key));
    }

    public ProcessSortKey Key { get; }
    public ICommand SortCommand { get; }

    /// <summary>True when the list is currently sorted by this column.</summary>
    [ObservableProperty] private bool _isActive;

    /// <summary>"↑" / "↓" when active, empty otherwise.</summary>
    [ObservableProperty] private string _arrow = "";
}
