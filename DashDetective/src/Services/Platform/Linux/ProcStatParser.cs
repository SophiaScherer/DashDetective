using System;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Parses <c>/proc/stat</c>'s <c>cpu</c> lines into busy/total jiffy counters. Shared format knowledge
/// rather than sampler logic, so it lives beside <see cref="IProcFileSystem"/> and serves both the
/// aggregate CPU sampler and the per-logical-processor one.
///
/// The counters are monotonic totals since boot, so a utilisation figure is always a diff between two
/// reads — see <see cref="ComputeUsage"/>.
/// </summary>
internal static class ProcStatParser {
    // Field indices within a cpu line, after the label. The kernel has appended columns over time, so a
    // parser must read by index with a length check rather than assume a count.
    private const int IdleIndex = 3;
    private const int IoWaitIndex = 4;
    private const int StealIndex = 7;

    // The shortest form worth trusting: a label plus user, nice, system and idle.
    private const int MinimumTokens = 5;

    /// <summary>
    /// Parses one line into its label ("cpu", "cpu0", …) and its busy/total jiffy counters. Returns false
    /// for a non-cpu line, a truncated one, or any non-numeric field.
    ///
    /// <c>iowait</c> counts as idle and <c>steal</c> counts as busy — steal is time the hypervisor took,
    /// which the guest did not spend idle, and counting it is what makes the reading agree with
    /// <c>top</c> in a VM. <c>guest</c> and <c>guest_nice</c> are excluded from the total because the
    /// kernel already folds them into <c>user</c> and <c>nice</c>; summing all ten double-counts them.
    /// </summary>
    internal static bool TryParseCpuLine(string line, out string label, out ulong busy, out ulong total) {
        label = "";
        busy = 0;
        total = 0;

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < MinimumTokens || !tokens[0].StartsWith("cpu", StringComparison.Ordinal))
            return false;

        ulong idle = 0;
        ulong sum = 0;
        for (var field = 0; field <= StealIndex && field + 1 < tokens.Length; field++) {
            if (!ulong.TryParse(tokens[field + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return false;

            sum += value;
            if (field is IdleIndex or IoWaitIndex)
                idle += value;
        }

        label = tokens[0];
        total = sum;
        busy = sum - idle; // idle is a subset of sum, so this cannot underflow
        return true;
    }

    /// <summary>Pure busy-fraction math over one interval's deltas, split out so it can be unit-tested
    /// with injected values — the same shape as <c>SystemTimesCpuSampler.ComputeUsage</c>. Returns 0 for
    /// an empty interval and clamps defensively.</summary>
    internal static double ComputeUsage(ulong busyDelta, ulong totalDelta) {
        if (totalDelta == 0)
            return 0;

        var usage = busyDelta * 100.0 / totalDelta;
        return usage < 0 ? 0 : usage > 100 ? 100 : usage;
    }
}
