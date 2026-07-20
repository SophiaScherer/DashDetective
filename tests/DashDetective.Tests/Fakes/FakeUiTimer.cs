using DashDetective.Services.Threading;
using System;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IUiTimer"/> for headless tests: records Start/Stop and fires <see cref="Tick"/>
/// only while running (a stopped timer never ticks, matching the real one). Tests call
/// <see cref="RaiseTick"/> to drive a tick deterministically.
/// </summary>
internal sealed class FakeUiTimer : IUiTimer {
    public TimeSpan Interval { get; set; }
    public bool IsRunning { get; private set; }
    public int StartCount { get; private set; }
    public int StopCount { get; private set; }

    public event EventHandler? Tick;

    public void Start() {
        IsRunning = true;
        StartCount++;
    }

    public void Stop() {
        IsRunning = false;
        StopCount++;
    }

    /// <summary>Fires one tick — but only while running, so a stopped channel stays quiet.</summary>
    public void RaiseTick() {
        if (IsRunning)
            Tick?.Invoke(this, EventArgs.Empty);
    }
}
