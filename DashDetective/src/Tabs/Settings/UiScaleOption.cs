using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// A selectable entry in the interface-size segmented control (100% … 200%). Mirrors
/// <see cref="ThemeOption"/>: an observable <see cref="IsSelected"/> flag plus a command that reports
/// the click back to the owning view-model.
/// </summary>
public partial class UiScaleOption : ObservableObject {
    public UiScaleOption(int percent, Action<UiScaleOption> onSelected) {
        Percent = percent;
        Label = $"{percent}%";
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }

    /// <summary>The scale as a percentage, applied through the accessibility service.</summary>
    public int Percent { get; }

    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
