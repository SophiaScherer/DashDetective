using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Shared.Charts;
using System.Collections.Generic;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// One small chart in a resource's "Detailed" view — a single logical processor (CPU) or GPU engine. Carries
/// a fixed <see cref="Label"/> plus the parent resource's <see cref="Stroke"/>; the owning
/// <see cref="PerformanceViewModel"/> rebuilds <see cref="Points"/> in place each sampling tick from its own
/// rolling history, and restates <see cref="Stroke"/> when the accent moves the palette.
/// </summary>
public partial class SubChart : ObservableObject {
    public SubChart(string label, IBrush stroke) {
        Label = label;
        Stroke = stroke;
    }

    /// <summary>Caption shown above the mini chart, e.g. "CPU 0" or "Video Decode".</summary>
    public string Label { get; }

    /// <summary>Line colour, matching the parent resource's <see cref="ResourceRow.ValueBrush"/>. Observable
    /// for the same reason that one is: the palette follows the accent.</summary>
    [ObservableProperty] private IBrush _stroke = Brushes.Transparent;

    /// <summary>The 60-point history as a Sparkline "x,y x,y …" string, live-updated each tick.</summary>
    [ObservableProperty] private string _points = "";

    /// <summary>The cell's value axis: its two ends and nothing between them. A cell is a third the height
    /// of the chart above it, so a third reading would crowd the two that bound it — and it carries no time
    /// row at all, the window being stated once in the caption over the grid.</summary>
    public IReadOnlyList<string> AxisLabels { get; } = ChartAxis.PercentLabels(1);
}
