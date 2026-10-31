namespace DashDetective.Tabs.Performance;

/// <summary>One static fact in the detail pane's key/value list, e.g. "Driver version" / "32.0.16.1062".
/// Unlike a <see cref="StatTile"/> it never changes after the row is built.</summary>
public sealed record DetailRow(string Key, string Value);
