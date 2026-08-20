using System;

namespace DashDetective.Shared;

/// <summary>
/// Composes a page's two on/off inputs — the toolbar's Live pill and whether the page is on screen —
/// into the single answer its timers care about, so the five sampling pages don't each hand-roll the
/// same pair of flags.
///
/// Starts <c>live</c> but <b>not</b> active: a page constructed by the shell samples nothing until it
/// is navigated to. <paramref name="apply"/> is invoked only on a TRANSITION, so repeated activations
/// (a tab re-selected, the Live pill toggled off-screen) can't churn the timers underneath.
/// </summary>
internal sealed class SamplingGate(Action<bool> apply) {
    private bool _live = true;
    private bool _active;

    /// <summary>Whether the Live pill is on.</summary>
    public bool Live {
        get => _live;
        set {
            _live = value;
            Evaluate();
        }
    }

    /// <summary>Whether the page is the visible one.</summary>
    public bool Active {
        get => _active;
        set {
            _active = value;
            Evaluate();
        }
    }

    /// <summary>Whether the page's sampling is currently running.</summary>
    public bool IsRunning { get; private set; }

    private void Evaluate() {
        var running = _live && _active;
        if (running == IsRunning)
            return;

        IsRunning = running;
        apply(running);
    }
}
