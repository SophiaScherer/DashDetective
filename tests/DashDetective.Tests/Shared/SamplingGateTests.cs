using DashDetective.Shared;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Pins <see cref="SamplingGate"/>'s contract: a page samples only when the Live pill is on AND
/// it is on screen, it starts idle so an unopened tab costs nothing, and the callback fires only on a
/// transition — repeated activations must not churn the page's timers.</summary>
public class SamplingGateTests {
    private static (SamplingGate Gate, List<bool> Applied) Create() {
        var applied = new List<bool>();
        return (new SamplingGate(applied.Add), applied);
    }

    [Fact]
    public void Construction_IsLiveButNotActive_SoNothingRuns() {
        var (gate, applied) = Create();

        Assert.True(gate.Live);
        Assert.False(gate.Active);
        Assert.False(gate.IsRunning);
        Assert.Empty(applied);
    }

    [Fact]
    public void Activate_WhileLive_Runs() {
        var (gate, applied) = Create();

        gate.Active = true;

        Assert.True(gate.IsRunning);
        Assert.Equal(new[] { true }, applied);
    }

    [Fact]
    public void Activate_WhilePaused_StaysStopped_AndResumesOnLive() {
        var (gate, applied) = Create();
        gate.Live = false;

        gate.Active = true;
        Assert.False(gate.IsRunning);
        Assert.Empty(applied);

        gate.Live = true;
        Assert.True(gate.IsRunning);
        Assert.Equal(new[] { true }, applied);
    }

    [Fact]
    public void Deactivate_StopsEvenWhileLive() {
        var (gate, applied) = Create();
        gate.Active = true;

        gate.Active = false;

        Assert.False(gate.IsRunning);
        Assert.Equal(new[] { true, false }, applied);
    }

    [Fact]
    public void RepeatedSetsOfTheSameState_DoNotReapply() {
        var (gate, applied) = Create();

        gate.Active = true;
        gate.Active = true;
        gate.Live = true;

        Assert.Equal(new[] { true }, applied);
    }

    [Fact]
    public void PillTogglingOffScreen_NeverReachesThePage() {
        var (gate, applied) = Create();

        gate.Live = false;
        gate.Live = true;
        gate.Live = false;

        Assert.Empty(applied);
        Assert.False(gate.IsRunning);
    }

    // ---- Token: the gate owns the lifetime of the work its timers start ----

    /// <summary>Before the page is ever shown, the only work that runs is its one-shot constructor load,
    /// which must complete: the exported report and universal search read from tabs the user may never
    /// open. So an idle gate hands out a token that is never cancelled.</summary>
    [Fact]
    public void Token_WhileIdle_IsNotCancellable() {
        var (gate, _) = Create();

        Assert.False(gate.Token.CanBeCanceled);
    }

    [Fact]
    public void Token_WhileRunning_IsLiveAndUncancelled() {
        var (gate, _) = Create();

        gate.Active = true;

        Assert.True(gate.Token.CanBeCanceled);
        Assert.False(gate.Token.IsCancellationRequested);
    }

    /// <summary>The defect this exists to fix: leaving the tab must abandon the read already in the air,
    /// so it cannot land — or write its "unavailable" fallback — into a page nobody is looking at.</summary>
    [Fact]
    public void Deactivating_CancelsTheTokenTheInFlightReadCaptured() {
        var (gate, _) = Create();
        gate.Active = true;
        var captured = gate.Token;

        gate.Active = false;

        Assert.True(captured.IsCancellationRequested);
    }

    [Fact]
    public void TurningTheLivePillOff_CancelsTheTokenToo() {
        var (gate, _) = Create();
        gate.Active = true;
        var captured = gate.Token;

        gate.Live = false;

        Assert.True(captured.IsCancellationRequested);
    }

    /// <summary>Coming back must not hand the page a token that is already cancelled, or every read after
    /// the first tab switch would be discarded and the page would never fill in again.</summary>
    [Fact]
    public void ReactivatingAfterAStop_MintsAFreshToken() {
        var (gate, _) = Create();
        gate.Active = true;
        var first = gate.Token;
        gate.Active = false;

        gate.Active = true;
        var second = gate.Token;

        Assert.True(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
        Assert.NotEqual(first, second);
    }

    /// <summary>A re-selected tab is not a transition, so it must not churn the token any more than it
    /// churns the timers — a read in flight across the re-selection keeps running.</summary>
    [Fact]
    public void RepeatedActivation_LeavesTheTokenAlone() {
        var (gate, _) = Create();
        gate.Active = true;
        var captured = gate.Token;

        gate.Active = true;

        Assert.False(captured.IsCancellationRequested);
        Assert.Equal(captured, gate.Token);
    }

    [Fact]
    public void Dispose_CancelsInFlightWorkAndIsSafeTwice() {
        var (gate, _) = Create();
        gate.Active = true;
        var captured = gate.Token;

        gate.Dispose();
        gate.Dispose();

        Assert.True(captured.IsCancellationRequested);
    }
}
