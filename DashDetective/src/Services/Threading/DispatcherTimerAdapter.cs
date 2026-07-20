using Avalonia.Threading;
using System;

namespace DashDetective.Services.Threading;

/// <summary>
/// The production <see cref="IUiTimer"/>: a thin pass-through to an Avalonia <see cref="DispatcherTimer"/>,
/// so live sampling keeps ticking on the UI thread exactly as before the seam was introduced.
/// </summary>
internal sealed class DispatcherTimerAdapter : IUiTimer {
    private readonly DispatcherTimer _timer = new();

    public TimeSpan Interval {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public event EventHandler? Tick {
        add => _timer.Tick += value;
        remove => _timer.Tick -= value;
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();
}
