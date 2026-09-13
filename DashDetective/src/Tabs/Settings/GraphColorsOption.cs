using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Theming;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// A selectable Graph colors choice. When <see cref="Colors"/> is <c>null</c> this is the "Default"
/// (multi-colour) option, rendered as a four-colour square; otherwise it is a single-hue swatch. Mirrors
/// the sidebar's <c>NavItem</c> selection pattern; the selection ring is styled in XAML.
/// </summary>
public partial class GraphColorsOption : ObservableObject {
    public GraphColorsOption(GraphColors? colors, Action<GraphColorsOption> onSelected) {
        Colors = colors;
        Swatch = colors is null ? null : new SolidColorBrush(colors.Hue);
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    /// <summary>The single-hue choice, or <c>null</c> for the Default option.</summary>
    public GraphColors? Colors { get; }

    /// <summary>True for the Default option (shows the four-colour legend).</summary>
    public bool IsDefault => Colors is null;

    /// <summary>The single-hue swatch fill; <c>null</c> for the Default option.</summary>
    [ObservableProperty] private IBrush? _swatch;

    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
