namespace DashDetective.Tabs.Performance;

/// <summary>
/// One numeric readout in the Performance detail pane's stat strip: a small label over a larger value
/// (e.g. "Utilization" / "23 %"). A plain display record — static mock data for now; live values are a
/// later technical pass.
/// </summary>
public sealed class StatTile {
    public StatTile(string label, string value) {
        Label = label;
        Value = value;
    }

    /// <summary>Small uppercase-style caption shown at the top of the tile.</summary>
    public string Label { get; }

    /// <summary>The headline value shown below the label.</summary>
    public string Value { get; }
}
