using System;
using System.Collections.Generic;
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
/// <see cref="ValueBrush"/> (a fixed legend colour, like <c>MainWindowViewModel</c>'s live dots).
///
/// The owning <see cref="PerformanceViewModel"/> updates the live members (<see cref="ValueText"/>,
/// <see cref="Sub"/>, <see cref="Spec"/>, <see cref="Points"/>, and each tile's value) in place each
/// sampling tick; <see cref="Name"/> / <see cref="Unit"/> / <see cref="ValueBrush"/> are fixed identity.
/// </summary>
public partial class ResourceRow : ObservableObject {
    public ResourceRow(string name, string sub, string spec, string valueText, string unit,
                       IBrush valueBrush, string points, IReadOnlyList<StatTile> stats,
                       Action<ResourceRow> onSelected) {
        Name = name;
        Sub = sub;
        Spec = spec;
        ValueText = valueText;
        Unit = unit;
        ValueBrush = valueBrush;
        Points = points;
        Stats = stats;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    /// <summary>Resource name shown on the card and as the detail header (e.g. "CPU", "Disk 0 (C:)").</summary>
    public string Name { get; }

    /// <summary>Secondary caption under the name (e.g. "24 cores · 3.2 GHz", "NVMe SSD"). Loaded from
    /// the resource's static-info provider once available.</summary>
    [ObservableProperty] private string _sub;

    /// <summary>Device/spec string shown at the right of the detail header (e.g. "Intel Core i9-14900K").
    /// Loaded from the resource's static-info provider once available.</summary>
    [ObservableProperty] private string _spec;

    /// <summary>Headline value shown at the right of the card (paired with <see cref="Unit"/>).
    /// Live-updated each sampling tick.</summary>
    [ObservableProperty] private string _valueText;

    /// <summary>Unit suffix for <see cref="ValueText"/> (e.g. "%", "Mbps"). Fixed for percentage
    /// metrics; the network row re-scales it (kbps / Mbps / Gbps) with the live rate.</summary>
    [ObservableProperty] private string _unit;

    /// <summary>Semantic per-metric tint for the value and the detail utilization chart. A fixed
    /// legend colour, so it is theme/accent-independent by design.</summary>
    public IBrush ValueBrush { get; }

    /// <summary>The 60-point utilization history for the detail chart, as a Sparkline "x,y x,y …" string
    /// (y already flipped to axis-max − value so higher utilization sits at the top). Live-updated each
    /// sampling tick.</summary>
    [ObservableProperty] private string _points;

    /// <summary>The four resource-specific readouts shown in the detail stat strip (per the design comp's
    /// statMap). The list is fixed; each tile's value is updated in place each sampling tick.</summary>
    public IReadOnlyList<StatTile> Stats { get; }

    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
