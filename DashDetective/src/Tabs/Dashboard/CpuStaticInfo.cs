using System;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// Static CPU hardware facts, read once at startup via WMI. Fields fall back to
/// <c>0</c> / "Unknown processor" when the information could not be retrieved.
/// </summary>
public sealed record CpuStaticInfo(string Name, int PhysicalCores, int LogicalCores, double MaxClockMhz) {
    /// <summary>
    /// Fallback used when WMI is unavailable. The logical-core count still comes from the
    /// runtime, which is reliable on any platform.
    /// </summary>
    public static CpuStaticInfo Unknown { get; } =
        new("Unknown processor", 0, Environment.ProcessorCount, 0);
}
