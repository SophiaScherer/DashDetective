using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Theming;
using System;
using System.Windows.Input;

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
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    /// <summary>Repaints the swatch for the theme in force. The two themes render an accent at different
    /// lightnesses, so a fixed swatch would advertise a color the app does not draw.</summary>
    public void Refresh(bool dark) =>
        Swatch = Preset is null ? null : new SolidColorBrush(Preset.For(dark).Fill);

    /// <summary>The single accent, or <c>null</c> for the default multi-colour option.</summary>
    public AccentPreset? Preset { get; }

    /// <summary>True for the default multi-colour option (shows the four-colour legend).</summary>
    public bool IsDefault => Preset is null;

    /// <summary>The single-colour swatch fill; <c>null</c> for the default option.</summary>
    [ObservableProperty] private IBrush? _swatch;

    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
