using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Parses <c>/proc/[pid]/io</c> into one cumulative byte total for the Disk column, which the caller diffs
/// across the sample interval to get a rate.
///
/// <b><c>rchar</c> + <c>wchar</c>, not <c>read_bytes</c> + <c>write_bytes</c>.</b> The Windows column is
/// built from <c>ReadTransferCount</c> + <c>WriteTransferCount</c>, which count bytes moved through the
/// syscall layer including those served from cache; <c>rchar</c>/<c>wchar</c> are the same measurement.
/// <c>read_bytes</c>/<c>write_bytes</c> count only real block-layer traffic, so using them would put a
/// systematically smaller number under a column heading that means something else on the other platform.
///
/// The file is mode <b>0400</b>: readable for your own processes, denied for root's and other users'. A
/// denial is a false return and a blank rate, never an error.
/// </summary>
internal static class ProcPidIoParser {
    /// <summary>The cumulative read+write byte total. False when neither counter was readable — which is
    /// the ordinary outcome for a process you do not own.</summary>
    internal static bool TryParse(IReadOnlyList<string> lines, out ulong totalBytes) {
        totalBytes = 0;
        var found = false;

        foreach (var line in lines) {
            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var key = line.AsSpan(0, colon).Trim();
            if (!key.Equals("rchar", StringComparison.Ordinal) && !key.Equals("wchar", StringComparison.Ordinal))
                continue;

            if (!ulong.TryParse(
                    line.AsSpan(colon + 1).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                continue;

            // Saturate rather than wrap; the two counters are independent and either may be huge.
            totalBytes = totalBytes <= ulong.MaxValue - value ? totalBytes + value : ulong.MaxValue;
            found = true;
        }

        return found;
    }
}
