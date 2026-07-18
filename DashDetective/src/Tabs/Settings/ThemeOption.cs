using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Theming;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// A selectable entry in the Theme segmented control (Dark / Light / System). Mirrors the
/// sidebar's <c>NavItem</c> pattern: an observable <see cref="IsSelected"/> flag plus a command
/// that reports the click back to the owning view-model.
/// </summary>
public partial class ThemeOption : ObservableObject {
    public ThemeOption(string label, AppTheme value, Action<ThemeOption> onSelected) {
        Label = label;
        Value = value;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }
    public AppTheme Value { get; }
    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
