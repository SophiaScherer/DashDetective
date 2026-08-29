using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Shared;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// A selectable entry in the clock-format segmented control (24-hour / 12-hour). Mirrors
/// <see cref="ThemeOption"/>: an observable <see cref="IsSelected"/> flag plus a command that reports
/// the click back to the owning view-model.
/// </summary>
public partial class ClockFormatOption : ObservableObject {
    public ClockFormatOption(string label, ClockFormat value, Action<ClockFormatOption> onSelected) {
        Label = label;
        Value = value;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }
    public ClockFormat Value { get; }
    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
