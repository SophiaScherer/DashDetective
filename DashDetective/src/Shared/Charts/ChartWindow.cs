using System;
using System.Globalization;

namespace DashDetective.Shared.Charts;

/// <summary>
/// The span of time a rolling chart actually covers. The buffers are a fixed number of samples, so the span
/// is the sampling interval multiplied by that count — it stretches from 30 seconds at the fastest Settings
/// cadence to 5 minutes at the slowest. Captions read through here rather than hardcoding "60 seconds",
/// which is only true at the default 1 Hz.
/// </summary>
public static class ChartWindow {
    /// <summary>The window <paramref name="samples"/> slots cover at <paramref name="interval"/>.</summary>
    public static TimeSpan Span(int samples, TimeSpan interval) =>
        samples > 0 ? interval * samples : TimeSpan.Zero;

    /// <summary>The oldest end of the range as an axis label, e.g. "−60s" or "−5m". Compact
    /// because it sits under the plot's left corner, where the caption's fuller wording would crowd the
    /// chart. Switches to minutes at the same 90-second mark <see cref="Describe"/> does.</summary>
    public static string StartLabel(int samples, TimeSpan interval) {
        var seconds = Span(samples, interval).TotalSeconds;
        return seconds < 90
            ? $"−{Math.Round(seconds).ToString(CultureInfo.InvariantCulture)}s"
            : $"−{Math.Round(seconds / 60, 1).ToString("0.#", CultureInfo.InvariantCulture)}m";
    }

    /// <summary>The newest end of the range. A constant, but it belongs beside its opposite number.</summary>
    public const string EndLabel = "now";

    /// <summary>The window as a caption fragment, e.g. "60 seconds" or "5 minutes". Whole minutes above
    /// 90 seconds (where a second count stops reading naturally), whole seconds below.</summary>
    public static string Describe(int samples, TimeSpan interval) {
        var seconds = Span(samples, interval).TotalSeconds;
        if (seconds < 90)
            return $"{Math.Round(seconds).ToString(CultureInfo.InvariantCulture)} seconds";

        var minutes = Math.Round(seconds / 60, 1);
        var plural = Math.Abs(minutes - 1) < 0.05 ? "minute" : "minutes";
        return $"{minutes.ToString("0.#", CultureInfo.InvariantCulture)} {plural}";
    }
}
