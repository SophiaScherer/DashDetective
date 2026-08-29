using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Theming;
using DashDetective.Shared.Charts;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// One selectable resource in the Performance tab's left rail (CPU / Memory / Disk / GPU / Ethernet).
/// Same selectable-item-VM shape as <c>NavItem</c> / <c>FileExplorer.FilterOption</c>: an
/// <see cref="IsSelected"/> flag the template styles off, plus a <see cref="SelectCommand"/> that routes
/// back to the owning view model for single-selection.
///
/// Carries the resource's display identity (<see cref="Name"/> / <see cref="Sub"/> / <see cref="Spec"/>),
/// its headline value (<see cref="ValueText"/> + <see cref="Unit"/>), and which graph it is
/// (<see cref="Series"/>), from which the owning view model resolves <see cref="ValueBrush"/>.
///
/// The owning <see cref="PerformanceViewModel"/> updates the live members (<see cref="ValueText"/>,
/// <see cref="Sub"/>, <see cref="Spec"/>, <see cref="Points"/>, and each tile's value) in place each
/// sampling tick; <see cref="Name"/> / <see cref="Unit"/> / <see cref="Series"/> are fixed identity.
/// </summary>
public partial class ResourceRow : ObservableObject {
    public ResourceRow(string name, string sub, string spec, string valueText, string unit,
                       ChartSeries series, string points, IReadOnlyList<StatTile> stats,
                       Action<ResourceRow> onSelected) {
        Name = name;
        Sub = sub;
        Spec = spec;
        ValueText = valueText;
        Unit = unit;
        Series = series;
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
    /// metrics; the network row re-scales it (kbps / Mbps / Gbps) with the live rate. Shared by both
    /// headline values, which is what makes the pair directly comparable.</summary>
    [ObservableProperty] private string _unit;

    /// <summary>The second headline value, for a resource whose figure is one of a pair (the adapter row's
    /// send beside its receive). Shown only when <see cref="HasSecondSeries"/> is true.</summary>
    [ObservableProperty] private string _valueText2 = "";

    /// <summary>Direction glyphs drawn before each headline value ("↓" / "↑"), matching the arrows on the
    /// Dashboard's network card. Empty for single-value rows, which draw neither.</summary>
    public string ValueGlyph { get; init; } = "";

    public string ValueGlyph2 { get; init; } = "";

    /// <summary>Which graph this row is, as fixed identity. The colour itself is not stored here: the
    /// view model resolves it from the current <see cref="ChartPalette"/> and re-applies it when the
    /// accent changes, so the Performance tab and the Dashboard cannot disagree about CPU's hue.</summary>
    public ChartSeries Series { get; }

    /// <summary>Tint for the value and the detail utilization chart. Observable because the palette moves
    /// with the accent; the view model owns every assignment.</summary>
    [ObservableProperty] private IBrush _valueBrush = Brushes.Transparent;

    /// <summary>The 60-point utilization history for the detail chart, as a Sparkline "x,y x,y …" string
    /// (y already flipped to axis-max − value so higher utilization sits at the top). Live-updated each
    /// sampling tick.</summary>
    [ObservableProperty] private string _points;

    /// <summary>Optional second series on the same axis, for a resource whose headline is one of a pair
    /// (the adapter row's upload beside its download). Empty for single-series resources, which then draw
    /// nothing for it.</summary>
    [ObservableProperty] private string _points2 = "";

    /// <summary>Which graph <see cref="Points2"/> is, or null for a single-series resource.</summary>
    public ChartSeries? Series2 { get; init; }

    /// <summary>Whether this row draws two series, and so needs a key to tell them apart. One line needs
    /// no legend — the panel header already names it.</summary>
    public bool HasSecondSeries => Series2 is not null;

    /// <summary>The legend entries for a two-series row, e.g. "Receive" / "Send". Ignored when
    /// <see cref="HasSecondSeries"/> is false.</summary>
    public string LegendLabel1 { get; init; } = "";

    public string LegendLabel2 { get; init; } = "";

    /// <summary>Tint for <see cref="Points2"/>. Null for single-series resources, which draw nothing for
    /// it. Set from <see cref="Series2"/> alongside <see cref="ValueBrush"/>.</summary>
    [ObservableProperty] private IBrush? _valueBrush2;

    /// <summary>What the chart plots, e.g. "% Utilization" or "Receive and send" — the caption's fixed half,
    /// so it never claims a scale the chart isn't drawn on.</summary>
    public string ChartSubject { get; init; } = "% Utilization";

    /// <summary>Caption under the chart header: the subject plus the window the buffer currently covers.
    /// Observable because the window changes with the Settings refresh interval.</summary>
    [ObservableProperty] private string _chartCaption = "";

    /// <summary>The chart's value labels. A percentage resource states a fixed 100 / 50 / 0; the network
    /// row rewrites them each tick, since its ceiling follows the traffic — which is why these are
    /// observable rather than fixed identity.</summary>
    [ObservableProperty] private string _axisMaxLabel = "100%";
    [ObservableProperty] private string _axisMidLabel = "50%";
    [ObservableProperty] private string _axisMinLabel = "0";

    /// <summary>The cold-start line, cleared as soon as this row has a trace to show. Starts set: no row
    /// has a sample before its first tick. The initializer is qualified because this property's own name
    /// shadows the class it comes from.</summary>
    [ObservableProperty] private string _chartStatus = Shared.Charts.ChartStatus.Collecting;

    /// <summary>The four resource-specific readouts shown in the detail stat strip (per the design comp's
    /// statMap). The list is fixed; each tile's value is updated in place each sampling tick.</summary>
    public IReadOnlyList<StatTile> Stats { get; }

    /// <summary>Why this resource shows "—" rather than a value, or "" when it has one. Set per tick for a
    /// GPU whose driver publishes no utilisation, so an honestly blank chart doesn't read as a broken one.
    /// </summary>
    [ObservableProperty] private string _note = "";

    /// <summary>Whether <see cref="Note"/> has anything to say — the visibility the templates bind, so the
    /// caption line takes no space on a resource that is reporting normally.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary><see cref="Note"/> as a tooltip, or null when there is nothing to say. Avalonia only skips
    /// a tooltip on null, so binding the bare string pops an empty one on every healthy resource.</summary>
    public string? NoteTip => HasNote ? Note : null;

    partial void OnNoteChanged(string value) {
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(NoteTip));
    }

    public ICommand SelectCommand { get; }

    /// <summary>Where this resource lives on another tab, or null for one with nowhere to go.</summary>
    public ResourceLink? Link { get; init; }

    /// <summary>Whether this resource offers a jump — the link button's visibility.</summary>
    public bool HasLink => Link is not null;

    [ObservableProperty] private bool _isSelected;

    /// <summary>Whether this resource offers an "Overall / Detailed" chart toggle (CPU logical processors,
    /// GPU engines). False for resources with only a single utilization series. Observable because the CPU row
    /// gains it once the per-core sampler enumerates its logical processors on the first tick.</summary>
    [ObservableProperty] private bool _supportsDetail;

    /// <summary>The "Detailed" segment's label, e.g. "Per core" (CPU) or "Per engine" (GPU). Pairs with the
    /// fixed "Overall" beside it, so the two segments read as one scale. Only meaningful when
    /// <see cref="SupportsDetail"/> is true.</summary>
    [ObservableProperty] private string _detailLabel = "";

    /// <summary>The per-subunit mini charts shown in the Detailed view (one per logical processor / engine).
    /// Empty unless <see cref="SupportsDetail"/> is true; each chart's points are live-updated by the owning
    /// view model. Observable so a lazily-built set (CPU cores) refreshes the bound grid.</summary>
    [ObservableProperty] private IReadOnlyList<SubChart> _subCharts = Array.Empty<SubChart>();

    /// <summary>Whether the Detailed (per-subunit) view is shown rather than the single overall chart.
    /// Persisted per category (CPU / GPU) by the shell.</summary>
    [ObservableProperty] private bool _isDetailed;
}
