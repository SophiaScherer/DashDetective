using System;

namespace DashDetective.Shared.Charts;

/// <summary>
/// A metric's rolling window, plus how much of it is real yet.
///
/// The buffers are allocated full of zeros and every chart drew all of them from the first tick, so a
/// freshly launched app showed a flat line pinned at zero for a whole minute — absent data rendered as
/// measured idle. Tracking the fill lets <see cref="Points"/> plot only the samples actually taken, so the
/// trace enters at the right edge and grows leftward as Task Manager's does, and lets a page tell an empty
/// chart from an idle one (see <see cref="ChartStatus"/>).
///
/// Owns the canonical rolling-window update: shift left by one, append at the end.
/// </summary>
public sealed class MetricHistory {
    private readonly double[] _values;

    public MetricHistory(int window) => _values = new double[Math.Max(0, window)];

    /// <summary>The whole buffer, oldest-first — including the slots no sample has reached, which read
    /// zero. A read-only view over the live array; valid only until the next <see cref="Push"/>.</summary>
    public ReadOnlySpan<double> Values => _values;

    /// <summary>How many slots hold a real sample. Saturates at <see cref="Window"/>.</summary>
    public int Filled { get; private set; }

    /// <summary>The number of slots — not the number of samples taken.</summary>
    public int Window => _values.Length;

    /// <summary>Appends one sample: shift left by one, write at the end.</summary>
    public void Push(double value) {
        if (_values.Length == 0)
            return;

        Array.Copy(_values, 1, _values, 0, _values.Length - 1);
        _values[^1] = value;
        if (Filled < _values.Length)
            Filled++;
    }

    /// <summary>The Sparkline points string for the samples taken so far, on a 0–<paramref name="valueMax"/>
    /// axis.</summary>
    public string Points(double valueMax) => SparklinePoints.Build(_values, valueMax, Filled);
}
