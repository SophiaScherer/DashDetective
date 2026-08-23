using DashDetective.Shared;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>
/// Covers <see cref="OverlapGuard"/>: while a run holds the guard a second caller is turned away, and
/// disposing the scope always frees it — including when the run failed, which is the case the
/// hand-rolled <c>try/finally</c> versions of this existed to get right.
/// </summary>
public class OverlapGuardTests {
    [Fact]
    public void TryEnter_WhenIdle_GrantsTheRun() {
        var guard = new OverlapGuard();

        using var run = guard.TryEnter();

        Assert.NotNull(run);
        Assert.True(guard.IsRunning);
    }

    [Fact]
    public void TryEnter_WhileARunHoldsIt_TurnsTheSecondCallerAway() {
        var guard = new OverlapGuard();

        using var first = guard.TryEnter();
        using var second = guard.TryEnter();

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void Dispose_FreesTheGuardForTheNextRun() {
        var guard = new OverlapGuard();

        guard.TryEnter()!.Dispose();

        Assert.False(guard.IsRunning);
        using var next = guard.TryEnter();
        Assert.NotNull(next);
    }

    /// <summary>The reason the scope is <c>IDisposable</c> rather than a flag the caller lowers: a run
    /// that throws must still free the guard, or the poll is dead for the rest of the session.</summary>
    [Fact]
    public void Dispose_FreesTheGuardEvenWhenTheRunThrew() {
        var guard = new OverlapGuard();

        try {
            using var run = guard.TryEnter();
            throw new System.InvalidOperationException("the read failed");
        } catch (System.InvalidOperationException) {
            // The caller's own soft-fail would go here.
        }

        Assert.False(guard.IsRunning);
        Assert.NotNull(guard.TryEnter());
    }

    /// <summary>The shape every caller actually uses: a tick arriving mid-read is dropped rather than
    /// queued, so a slow provider cannot pile up work behind itself.</summary>
    [Fact]
    public async Task ConcurrentPolls_RunOnceRatherThanPilingUp() {
        var guard = new OverlapGuard();
        var gate = new TaskCompletionSource();
        var runs = 0;

        async Task PollAsync() {
            using var run = guard.TryEnter();
            if (run is null)
                return;

            runs++;
            await gate.Task;
        }

        var first = PollAsync();      // claims the guard and parks on the gate
        await PollAsync();            // arrives mid-read: must be dropped, not queued
        await PollAsync();

        Assert.Equal(1, runs);

        gate.SetResult();
        await first;

        // Once the first run completes the guard is free again, so the next tick does run.
        await PollAsync();
        Assert.Equal(2, runs);
    }
}
