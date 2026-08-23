using System;
using System.Globalization;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Parses a Windows <c>GPU Engine</c> performance-counter instance name — the
/// <c>pid_1234_luid_0x0_0xe54b_phys_0_eng_0_engtype_3D</c> shape — into the two fields the Processes
/// tab's GPU column needs: the owning PID and the engine type.
///
/// Pure, and deliberately NOT inside the Windows-gated sampler that consumes it. The counter name is a
/// string format, not an API call: keeping it here means it can be unit-tested on any host, where a
/// method on the sampler would be reachable only behind a platform guard and so would go unrun on the
/// Linux CI leg. Same reasoning as <c>GpuAdapter.FormatLuidToken</c>, which lives on the adapter model
/// rather than on the DXGI reader.
/// </summary>
internal static class GpuEngineInstanceName {
    private const string PidToken = "pid_";
    private const string EngineToken = "engtype_";

    /// <summary>Pulls the PID (digits after <c>pid_</c>) and engine type (after the <b>last</b>
    /// <c>engtype_</c>) out of an instance name. False when there is no usable PID, in which case the
    /// counter is ignored rather than attributed to a guess.</summary>
    public static bool TryParse(string? instanceName, out int pid, out string engine) {
        pid = 0;
        engine = "";
        if (string.IsNullOrEmpty(instanceName))
            return false;

        var pidIdx = instanceName.IndexOf(PidToken, StringComparison.Ordinal);
        if (pidIdx < 0)
            return false;

        var start = pidIdx + PidToken.Length;
        var end = start;
        while (end < instanceName.Length && char.IsDigit(instanceName[end]))
            end++;
        if (end == start ||
            !int.TryParse(instanceName.AsSpan(start, end - start), NumberStyles.Integer,
                          CultureInfo.InvariantCulture, out pid))
            return false;

        // Searched from the END: a LUID can contain the token's own letters, and scanning forwards would
        // slice the engine name out of the middle of the adapter id.
        var engIdx = instanceName.LastIndexOf(EngineToken, StringComparison.Ordinal);
        engine = engIdx < 0 ? "" : instanceName[(engIdx + EngineToken.Length)..];
        return true;
    }
}
