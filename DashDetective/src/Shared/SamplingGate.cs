using System;
using System.Threading;

namespace DashDetective.Shared;

/// <summary>
/// Composes a page's two on/off inputs — the toolbar's Live pill and whether the page is on screen —
/// into the single answer its timers care about, so the five sampling pages don't each hand-roll the
/// same pair of flags.
///
/// Starts <c>live</c> but <b>not</b> active: a page constructed by the shell samples nothing until it
/// is navigated to. <paramref name="apply"/> is invoked only on a TRANSITION, so repeated activations
/// (a tab re-selected, the Live pill toggled off-screen) can't churn the timers underneath.
///
/// <para>The gate also owns the lifetime of the work its timers start, via <see cref="Token"/>: stopping
/// a timer says nothing about the read already in the air, which would otherwise land — and, if it
/// failed, write its "unavailable" fallback — into a page nobody is looking at. The user would then see
/// that stale failure the next time they opened the tab.</para>
/// </summary>
internal sealed class SamplingGate(Action<bool> apply) : IDisposable {
    private bool _live = true;
    private bool _active;
    private CancellationTokenSource? _running;

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

    /// <summary>
    /// Cancelled when sampling stops. Pass it to the reads a page's timers start, so a tab switch
    /// abandons them instead of letting them land off-screen.
    ///
    /// <para>While sampling is stopped this is <see cref="CancellationToken.None"/> — deliberately: the
    /// only work that starts then is a page's one-shot constructor load, which must run to completion
    /// because the exported report and universal search read from tabs the user may never open.</para>
    /// </summary>
    public CancellationToken Token => _running?.Token ?? CancellationToken.None;

    /// <summary>Cancels any in-flight work. Safe to call more than once.</summary>
    public void Dispose() {
        var stopping = _running;
        _running = null;
        stopping?.Cancel();
        stopping?.Dispose();
    }

    private void Evaluate() {
        var running = _live && _active;
        if (running == IsRunning)
            return;

        IsRunning = running;

        // Minted before apply, because apply(true) may itself start a read that wants the token; and
        // cancelled before apply(false), so work already in the air is told to give up rather than
        // racing the timers being stopped.
        if (running)
            _running = new CancellationTokenSource();
        else
            Dispose();

        apply(running);
    }
}
