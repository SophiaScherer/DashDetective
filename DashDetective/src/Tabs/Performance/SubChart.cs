using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// One small chart in a resource's "Detailed" view — a single logical processor (CPU) or GPU engine. Carries
/// a fixed <see cref="Label"/> and <see cref="Stroke"/> (the parent resource's semantic colour); the owning
/// <see cref="PerformanceViewModel"/> rebuilds <see cref="Points"/> in place each sampling tick from its own
/// rolling history.
/// </summary>
public partial class SubChart : ObservableObject {
    public SubChart(string label, IBrush stroke) {
        Label = label;
        Stroke = stroke;
    }

    /// <summary>Caption shown above the mini chart, e.g. "CPU 0" or "Video Decode".</summary>
    public string Label { get; }

    /// <summary>Line colour, matching the parent resource's <see cref="ResourceRow.ValueBrush"/>.</summary>
    public IBrush Stroke { get; }

    /// <summary>The 60-point history as a Sparkline "x,y x,y …" string, live-updated each tick.</summary>
    [ObservableProperty] private string _points = "";
}
