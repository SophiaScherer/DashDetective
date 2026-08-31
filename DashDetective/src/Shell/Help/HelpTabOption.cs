using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Shell.Help;

/// <summary>
/// A selectable entry in the Help modal's segmented tab strip. Mirrors <c>ClockFormatOption</c>: an
/// observable <see cref="IsSelected"/> flag plus a command that reports the click back to the owning
/// view-model.
/// </summary>
public partial class HelpTabOption : ObservableObject {
    public HelpTabOption(string label, HelpTab value, Action<HelpTabOption> onSelected) {
        Label = label;
        Value = value;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }
    public HelpTab Value { get; }
    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
