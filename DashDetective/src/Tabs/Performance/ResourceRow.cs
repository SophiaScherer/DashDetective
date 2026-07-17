using System;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// One selectable resource in the Performance tab's left rail (CPU / Memory / Disk / GPU / Ethernet).
/// Same selectable-item-VM shape as <c>NavItem</c> / <c>FileExplorer.FilterOption</c>: an
/// <see cref="IsSelected"/> flag the template styles off, plus a <see cref="SelectCommand"/> that routes
/// back to the owning view model for single-selection.
///
/// Carries the resource's display identity (<see cref="Name"/> / <see cref="Sub"/> / <see cref="Spec"/>),
/// its headline value (<see cref="ValueText"/> + <see cref="Unit"/>), and the semantic per-metric
/// <see cref="ValueBrush"/> (a fixed legend colour, like <c>MainWindowViewModel</c>'s live dots). The
/// utilization-chart points and stat tiles are added in later UI phases; live values are a later
/// technical pass — everything here is static mock data for now.
/// </summary>
public partial class ResourceRow : ObservableObject {
    public ResourceRow(string name, string sub, string spec, string valueText, string unit,
                       IBrush valueBrush, Action<ResourceRow> onSelected) {
        Name = name;
        Sub = sub;
        Spec = spec;
        ValueText = valueText;
        Unit = unit;
        ValueBrush = valueBrush;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    /// <summary>Resource name shown on the card and as the detail header (e.g. "CPU", "Disk 0 (C:)").</summary>
    public string Name { get; }

    /// <summary>Secondary caption under the name (e.g. "24 cores · 3.2 GHz", "NVMe SSD").</summary>
    public string Sub { get; }

    /// <summary>Device/spec string shown at the right of the detail header (e.g. "Intel Core i9-14900K").</summary>
    public string Spec { get; }

    /// <summary>Headline value shown at the right of the card (paired with <see cref="Unit"/>).</summary>
    public string ValueText { get; }

    /// <summary>Unit suffix for <see cref="ValueText"/> (e.g. "%", "Mbps").</summary>
    public string Unit { get; }

    /// <summary>Semantic per-metric tint for the value (and the detail chart in a later phase). A fixed
    /// legend colour, so it is theme/accent-independent by design.</summary>
    public IBrush ValueBrush { get; }

    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
