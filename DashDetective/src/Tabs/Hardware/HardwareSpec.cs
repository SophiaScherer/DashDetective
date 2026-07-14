namespace DashDetective.Tabs.Hardware;

/// <summary>
/// One key/value spec row inside a hardware card (e.g. "Cores / Threads" → "24 / 32"). The value
/// defaults to the neutral placeholder "—" for this UI-only phase; a later technical phase fills it
/// from a real reading.
/// </summary>
public sealed record HardwareSpec(string Key, string Value = "—");
