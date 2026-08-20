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
}
