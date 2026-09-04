using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Theming;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// A selectable entry in the color-vision segmented control. Mirrors <see cref="ThemeOption"/>: an
/// observable <see cref="IsSelected"/> flag plus a command that reports the click back to the owning
/// view-model.
/// </summary>
public partial class ColorVisionOption : ObservableObject {
    public ColorVisionOption(string label, ColorVisionMode value, Action<ColorVisionOption> onSelected) {
        Label = label;
        Value = value;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }
    public ColorVisionMode Value { get; }
    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
