using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Shared;

/// <summary>
/// The key-agnostic half of a sortable table column, so a header control can bind to one without
/// knowing which key vocabulary the table sorts by. Bind headers to this; construct
/// <see cref="SortColumn{TKey}"/>.
/// </summary>
public abstract partial class SortColumn : ObservableObject {
    protected SortColumn(ICommand sortCommand) => SortCommand = sortCommand;

    /// <summary>Sorts the table by this column.</summary>
    public ICommand SortCommand { get; }

    /// <summary>True when the table is currently sorted by this column.</summary>
    [ObservableProperty] private bool _isActive;

    /// <summary>"↑" / "↓" when active, empty otherwise.</summary>
    [ObservableProperty] private string _arrow = "";
}

/// <summary>
/// A clickable table column header. Clicking sorts by its <see cref="Key"/>; the active column shows a
/// direction arrow and is tinted. File Explorer and Processes both list one per column, keyed by their
/// own sort-key enums.
/// </summary>
public sealed class SortColumn<TKey> : SortColumn {
    public SortColumn(TKey key, Action<TKey> onSort) : base(new RelayCommand(() => onSort(key))) =>
        Key = key;

    /// <summary>Which column this is, in the table's own sort-key vocabulary.</summary>
    public TKey Key { get; }
}
