using System;
using System.Collections.Generic;
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

    /// <summary>The oldest end of the range as an axis label, e.g. "60s ago" or "5m ago". Said as elapsed
    /// time rather than as a negative offset: a leading minus reads as a value below zero, which on a chart
    /// whose y axis starts at zero is exactly the wrong thing to suggest. Compact because it sits under the
    /// plot's left corner, where the caption's fuller wording would crowd the chart. Switches to minutes at
    /// the same 90-second mark <see cref="Describe"/> does.</summary>
    public static string StartLabel(int samples, TimeSpan interval) {
        var seconds = Span(samples, interval).TotalSeconds;
        return seconds < 90
            ? $"{Math.Round(seconds).ToString(CultureInfo.InvariantCulture)}s ago"
            : $"{Math.Round(seconds / 60, 1).ToString("0.#", CultureInfo.InvariantCulture)}m ago";
    }

    /// <summary>The newest end of the range. A constant, but it belongs beside its opposite number.</summary>
    public const string EndLabel = "now";

    /// <summary>The time axis for a chart ruled into <paramref name="divisions"/> bands, oldest first, so a
    /// point partway along the plot can be placed rather than estimated between the two ends. "ago" rides on
    /// the leftmost label only — the rest inherit its sense, the way a unit rides on one value label — and
    /// the whole row reads in one unit, picked from the span at the same 90-second mark
    /// <see cref="StartLabel"/> uses.</summary>
    public static IReadOnlyList<string> TickLabels(int samples, TimeSpan interval, int divisions) {
        var bands = Math.Max(1, divisions);
        var seconds = Span(samples, interval).TotalSeconds;
        var labels = new string[bands + 1];

        labels[0] = StartLabel(samples, interval);
        for (var i = 1; i < bands; i++) {
            var elapsed = seconds * (bands - i) / bands;
            labels[i] = seconds < 90
                ? $"{Math.Round(elapsed).ToString(CultureInfo.InvariantCulture)}s"
                : $"{Math.Round(elapsed / 60, 1).ToString("0.#", CultureInfo.InvariantCulture)}m";
        }
        labels[bands] = EndLabel;
        return labels;
    }

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
