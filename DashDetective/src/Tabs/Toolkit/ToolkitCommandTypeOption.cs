using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// One choice in the "+ Add command" form's type picker. Same selectable-item-VM shape as
/// <see cref="ToolkitCategoryOption"/> — segmented chips rather than a drop-down, which is how every
/// short pick in this app is made.
/// </summary>
public partial class ToolkitCommandTypeOption : ObservableObject {
    public ToolkitCommandTypeOption(ToolkitCommandType type, Action<ToolkitCommandTypeOption> onSelected) {
        Type = type;
        Label = ToolkitCommandFactory.LabelFor(type);
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public ToolkitCommandType Type { get; }
    public string Label { get; }
    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
