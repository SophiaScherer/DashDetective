using System;

namespace DashDetective.Services.Threading;

/// <summary>
/// Minimal seam over a UI-thread timer (Avalonia's <c>DispatcherTimer</c>), so timer-driven code can be
/// exercised headlessly. A real <c>DispatcherTimer</c> only raises <see cref="Tick"/> while an Avalonia
/// dispatcher is pumping, which no plain unit-test host provides; injecting a fake lets tests drive ticks
/// deterministically. Production always uses <see cref="DispatcherTimerAdapter"/> (the default), so
/// behaviour is unchanged — this exists purely for testability.
/// </summary>
internal interface IUiTimer {
    /// <summary>The interval between ticks. Takes effect on the next tick, like the underlying timer.</summary>
    TimeSpan Interval { get; set; }

    /// <summary>Raised once per interval while the timer is running.</summary>
    event EventHandler? Tick;

    /// <summary>Starts (or resumes) ticking.</summary>
    void Start();

    /// <summary>Stops ticking. A stopped timer never raises <see cref="Tick"/>.</summary>
    void Stop();
}
