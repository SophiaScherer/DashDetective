using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="MetricChannel"/> via the injected <see cref="FakeUiTimer"/> seam:
/// <c>SampleNow</c> while stopped, the rolling-history shift, and a sampler fault firing <c>onFailed</c>
/// once then permanently stopping the timer.</summary>
public class MetricChannelTests {
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    [Fact]
    public void SampleNow_WhileStopped_InvokesOnSampleAndUpdatesHistory() {
        var fake = new FakeUiTimer();
        double? received = null;
        var channel = new MetricChannel(Interval, 3, () => 42.0, v => received = v, () => { }, fake);

        channel.SampleNow();

        Assert.Equal(42.0, received);
        Assert.Equal(42.0, channel.History[^1]);
        Assert.False(fake.IsRunning);   // SampleNow never starts the timer
    }

    [Fact]
    public void SampleNow_ShiftsHistoryOldestFirst() {
        var next = 0.0;
        var channel = new MetricChannel(Interval, 3, () => next, _ => { }, () => { }, new FakeUiTimer());

        foreach (var v in new[] { 1.0, 2.0, 3.0, 4.0 }) {
            next = v;
            channel.SampleNow();
        }

        Assert.Equal(new[] { 2.0, 3.0, 4.0 }, channel.History.ToArray());
    }

    [Fact]
    public void Tick_WhileRunning_DeliversSampleAndPushesHistory() {
        var fake = new FakeUiTimer();
        double? received = null;
        var channel = new MetricChannel(Interval, 2, () => 9.0, v => received = v, () => { }, fake);

        channel.Start();
        fake.RaiseTick();

        Assert.Equal(9.0, received);
        Assert.Equal(9.0, channel.History[^1]);
    }

    [Fact]
    public void SamplerException_OnTick_FiresOnFailedOnceThenStopsPermanently() {
        var fake = new FakeUiTimer();
        var failedCount = 0;
        var channel = new MetricChannel(Interval, 0,
            () => throw new InvalidOperationException("counter gone"),
            _ => { }, () => failedCount++, fake);

        channel.Start();
        Assert.True(fake.IsRunning);

        fake.RaiseTick();   // sample throws → onFailed + Stop
        Assert.Equal(1, failedCount);
        Assert.False(fake.IsRunning);

        fake.RaiseTick();   // timer is stopped → no further ticks reach the channel
        Assert.Equal(1, failedCount);
    }

    [Fact]
    public void SampleNow_AfterFaultStop_StillSamples() {
        // Documented nuance: the permanent stop is on the timer; SampleNow bypasses it.
        var fake = new FakeUiTimer();
        var throwNow = true;
        double? received = null;
        var channel = new MetricChannel(Interval, 0,
            () => throwNow ? throw new InvalidOperationException() : 7.0,
            v => received = v, () => { }, fake);

        channel.Start();
        fake.RaiseTick();          // fault stops the timer
        Assert.False(fake.IsRunning);

        throwNow = false;
        channel.SampleNow();       // still samples despite the stop
        Assert.Equal(7.0, received);
    }

    [Fact]
    public void ZeroWindow_KeepsNoHistory() {
        double? received = null;
        var channel = new MetricChannel(Interval, 0, () => 5.0, v => received = v, () => { }, new FakeUiTimer());

        channel.SampleNow();

        Assert.True(channel.History.IsEmpty);
        Assert.Equal(5.0, received);
    }

    [Fact]
    public void PushHistory_ShiftsLeftAndAppends() {
        var buffer = new[] { 1.0, 2.0, 3.0 };
        MetricChannel<double>.PushHistory(buffer, 4.0);
        Assert.Equal(new[] { 2.0, 3.0, 4.0 }, buffer);
    }

    [Fact]
    public void PushHistory_EmptyBuffer_IsNoOp() {
        var empty = Array.Empty<double>();
        MetricChannel<double>.PushHistory(empty, 5.0);
        Assert.Empty(empty);
    }
}
