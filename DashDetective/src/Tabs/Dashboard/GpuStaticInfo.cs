namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// Static GPU hardware facts, read once at startup via WMI. <see cref="Name"/> falls back to
/// "Unknown GPU" when the information could not be retrieved.
/// </summary>
public sealed record GpuStaticInfo(string Name) {
    /// <summary>Fallback used when WMI is unavailable or returns nothing usable.</summary>
    public static GpuStaticInfo Unknown { get; } = new("Unknown GPU");
}
