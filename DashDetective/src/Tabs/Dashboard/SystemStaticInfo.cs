using System;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// Static machine-identity facts for the System Information panel, read once at startup via WMI,
/// the registry and the runtime. Fields are display-ready strings and fall back to an "Unknown …"
/// label when the underlying source could not be read.
/// </summary>
public sealed record SystemStaticInfo(
    string Os, string Device, string Bios, string Build, string Motherboard) {
    /// <summary>
    /// Fallback used when the sources are unavailable. The device name still comes from the
    /// runtime, which is reliable on any platform.
    /// </summary>
    public static SystemStaticInfo Unknown { get; } =
        new("Unknown OS", Environment.MachineName, "Unknown BIOS", "Unknown", "Unknown motherboard");
}
