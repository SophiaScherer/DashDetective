namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Explains why a GPU shows "—" instead of a utilisation figure. A detected adapter whose driver publishes
/// no reading is a real state, not a failure (a VM's paravirtual GPU, Intel's i915, the proprietary NVIDIA
/// blob), but a card of bare dashes reads as a broken feature — this is the sentence that tells them apart.
///
/// Pure; the caller decides where to show it. Only reachable on Linux today, since the Windows PDH counter
/// always fills a figure for any adapter the inventory kept.
/// </summary>
public static class GpuNoReadingNote {
    /// <summary>PCI vendor id for NVIDIA — the one vendor with an opt-in source to point at.</summary>
    private const uint NvidiaVendorId = 0x10DE;

    /// <summary>The note for an adapter that reported no utilisation, given its PCI vendor and whether the
    /// NVIDIA opt-in is on. The NVIDIA arm splits on that flag so an enabled setting is never advertised as
    /// the fix.</summary>
    public static string For(uint? vendorId, bool nvidiaMetricsEnabled) {
        if (vendorId != NvidiaVendorId)
            return "This GPU's driver publishes no utilization figure.";

        return nvidiaMetricsEnabled
            ? "nvidia-smi reported no utilization for this card."
            : "Turn on \"NVIDIA GPU utilization\" in Settings to read this card.";
    }
}
