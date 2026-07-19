using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// A file-list filter chip (All / Documents / Images / Archives). A null <see cref="Category"/>
/// means "All". Same selectable-item-VM shape as ThemeOption / NavItem.
/// </summary>
public partial class FilterOption : ObservableObject {
    public FilterOption(string label, FileCategory? category, Action<FilterOption> onSelected) {
        Label = label;
        Category = category;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }
    public FileCategory? Category { get; }
    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
