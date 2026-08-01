namespace DashDetective.Shared;

/// <summary>
/// Chooses which of the two speeds <c>Win32_PhysicalMemory</c> reports to display.
/// <c>ConfiguredClockSpeed</c> is what the module is actually running at — the figure Task Manager shows —
/// while <c>Speed</c> is the rated one printed on the part, which reads higher whenever XMP/EXPO is off.
///
/// Shared because two tabs read the same modules: the Dashboard/Performance spec line and the Hardware
/// tab's Speed row. They previously each picked differently, so the same stick could be described two ways
/// on one machine.
/// </summary>
public static class MemorySpeed {
    /// <summary>The speed to show, in MT/s: the configured (running) figure, falling back to the rated one
    /// when the machine doesn't report it, and 0 when neither is available.</summary>
    public static int Running(int configuredMhz, int ratedMhz) =>
        configuredMhz > 0 ? configuredMhz : ratedMhz > 0 ? ratedMhz : 0;
}
