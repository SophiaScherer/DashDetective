using System;

namespace DashDetective.Shared;

/// <summary>
/// Refuses a second run of a poll while one is still in flight, so a slow read cannot pile up ticks
/// behind it. Three pages had hand-rolled the same bool-and-<c>finally</c> and drifted apart doing it.
///
/// This is deliberately <b>last-write-loses</b>: a tick arriving mid-read is dropped, because the run
/// already under way will report the same thing a moment later. That is right for a timer poll and
/// wrong for user-driven work, where the newest request is the one that matters — File Explorer's
/// folder load uses a generation counter to get last-write-<i>wins</i> instead, and the Toolkit's run
/// command uses <c>[RelayCommand(AllowConcurrentExecutions = false)]</c> so its busy state reaches the
/// UI. Those are different answers to different questions, not copies of this one.
///
/// <para><b>Not thread-safe, and does not need to be.</b> Every caller is UI-thread-affine: the poll is
/// started from a <c>DispatcherTimer</c> tick or a command, and awaits on the UI thread, so the
/// test-and-set cannot interleave. A guard reached from a threadpool callback would need
/// <c>Interlocked</c> — see <c>NvidiaSmiReader</c>, which locks for exactly that reason.</para>
/// </summary>
/// <example>
/// <code>
/// using var run = _guard.TryEnter();
/// if (run is null)
///     return;
/// try { /* read and apply */ } catch { /* this caller's own fallback */ }
/// </code>
/// </example>
internal sealed class OverlapGuard {
    private bool _running;

    /// <summary>Whether a run is currently in flight.</summary>
    public bool IsRunning => _running;

    /// <summary>Claims the guard, returning a scope to dispose when the run finishes — or
    /// <c>null</c> if a run is already in flight, meaning the caller should return.</summary>
    public IDisposable? TryEnter() => _running ? null : new Scope(this);

    private sealed class Scope : IDisposable {
        private readonly OverlapGuard _guard;

        internal Scope(OverlapGuard guard) {
            _guard = guard;
            guard._running = true;
        }

        public void Dispose() => _guard._running = false;
    }
}
