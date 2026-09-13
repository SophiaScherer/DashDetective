using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Theming;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// One swatch on the Accent color row: a preset, or Custom when <see cref="Preset"/> is <c>null</c>.
/// The <c>GraphColorsOption</c> selection pattern; the ring is styled in XAML.
/// </summary>
public partial class AccentSwatchOption : ObservableObject {
    public AccentSwatchOption(AccentPreset? preset, Action<AccentSwatchOption> onSelected) {
        Preset = preset;
        Swatch = preset is null ? ColorWheel.HueRing : new SolidColorBrush(preset.Identity);
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    /// <summary>The preset, or <c>null</c> for Custom.</summary>
    public AccentPreset? Preset { get; }

    public bool IsCustom => Preset is null;

    public string Name => Preset?.Name ?? AccentPreset.CustomName;

    /// <summary>The preset's color, or the hue sweep for Custom.</summary>
    public IBrush Swatch { get; }

    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
