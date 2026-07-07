namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// Static physical-memory hardware facts, read once at startup via WMI. Fields fall back to
/// <c>0</c> / "RAM" when the information could not be retrieved.
/// </summary>
public sealed record MemoryStaticInfo(double TotalGb, string TypeLabel, int SpeedMhz, int ModuleCount) {
    /// <summary>Fallback used when WMI is unavailable or returns nothing usable.</summary>
    public static MemoryStaticInfo Unknown { get; } = new(0, "RAM", 0, 0);
}
