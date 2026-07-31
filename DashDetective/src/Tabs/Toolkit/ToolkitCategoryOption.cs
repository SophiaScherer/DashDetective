using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// A category filter chip (All / File Locations / System Tools / …). A null <see cref="Category"/>
/// means "All". Same selectable-item-VM shape as FilterOption / ThemeOption / NavItem.
/// </summary>
public partial class ToolkitCategoryOption : ObservableObject {
    public ToolkitCategoryOption(string label, ToolkitCategory? category, Action<ToolkitCategoryOption> onSelected) {
        Label = label;
        Category = category;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }
    public ToolkitCategory? Category { get; }
    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
