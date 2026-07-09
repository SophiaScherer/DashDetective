using System;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Theming;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// A selectable accent choice. When <see cref="Preset"/> is <c>null</c> this is the "Default"
/// (multi-colour) option — rendered as a four-colour square that restores the default look;
/// otherwise it is a single-colour swatch. Mirrors the sidebar's <c>NavItem</c> selection pattern;
/// the selection ring is styled in XAML (theme-aware) rather than here.
/// </summary>
public partial class AccentOption : ObservableObject {
    public AccentOption(AccentPreset? preset, Action<AccentOption> onSelected) {
        Preset = preset;
        Swatch = preset is null ? null : new SolidColorBrush(preset.Color);
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    /// <summary>The single accent, or <c>null</c> for the default multi-colour option.</summary>
    public AccentPreset? Preset { get; }

    /// <summary>True for the default multi-colour option (shows the four-colour legend).</summary>
    public bool IsDefault => Preset is null;

    /// <summary>The single-colour swatch fill; <c>null</c> for the default option.</summary>
    public IBrush? Swatch { get; }

    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
