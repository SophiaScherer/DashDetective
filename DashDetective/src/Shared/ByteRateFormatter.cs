using System;

namespace DashDetective.Shared;

/// <summary>
/// Shared formatting for disk throughput given in bytes per second. Picks KB/s, MB/s or GB/s by magnitude
/// on a binary (1024) base, matching Task Manager's disk pane ("0 KB/s", "8.0 KB/s", "1.2 MB/s"); network
/// rates are bits on a decimal base and stay in <see cref="DataRateFormatter"/>.
/// </summary>
public static class ByteRateFormatter {
    private static readonly string[] Labels = ["KB/s", "MB/s", "GB/s"];

    /// <summary>Formats a rate as "value unit". The unit is chosen from the value as it will be displayed,
    /// so a rate that would round to "1024 KB/s" reads "1.0 MB/s" and the number never jumps at the step.
    /// Negative, NaN and infinite input, and a trickle that would display as "0.0", read as a plain zero.</summary>
    public static string Format(double bytesPerSec) {
        var value = double.IsFinite(bytesPerSec) ? bytesPerSec / 1024 : 0;
        // Below 0.05 KB/s FormatValue prints "0.0"; a near-idle disk would flicker between that and "0".
        if (value < 0.05)
            return $"0 {Labels[0]}";

        var unit = 0;
        // Math.Round mirrors FormatValue's whole-number rounding, which is what the reader sees at ≥ 10.
        while (unit < Labels.Length - 1 && Math.Round(value) >= 1024) {
            value /= 1024;
            unit++;
        }

        return $"{DataRateFormatter.FormatValue(value)} {Labels[unit]}";
    }
}
