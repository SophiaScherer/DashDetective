using CommunityToolkit.Mvvm.ComponentModel;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// One numeric readout in the Performance detail pane's stat strip: a small label over a larger value
/// (e.g. "Utilization" / "23 %"). The <see cref="Label"/> is fixed identity; the <see cref="Value"/> is
/// updated in place each sampling tick (the stat strip keeps binding the same tile instances).
/// </summary>
public partial class StatTile : ObservableObject {
    public StatTile(string label, string value) {
        Label = label;
        Value = value;
    }

    /// <summary>Small uppercase-style caption shown at the top of the tile.</summary>
    public string Label { get; }

    /// <summary>The headline value shown below the label. Live-updated by the owning view model.</summary>
    [ObservableProperty] private string _value;
}
