using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Threading;
using System;
using System.Collections.Generic;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// A <see cref="SystemMetricsService"/> over fake samplers and stepped timers — the four-lambda
/// <c>MetricSamplers</c> plus <c>() =&gt; new FakeUiTimer()</c> that the Dashboard, Storage and Processes
/// test files each wrote out verbatim.
///
/// <see cref="Idle"/> is for the pages that only need a service to exist. <see cref="WithTimers"/> hands
/// back the timers it minted, which is the only way to step a feed: a page attaches its subscriptions on
/// activation, and the failure callbacks fire on a tick, so a test that wants to see a dead counter has
/// to raise one.
/// </summary>
internal static class TestMetrics {
    /// <summary>Feeds that report a flat zero and never fail.</summary>
    public static SystemMetricsService Idle() => WithTimers(out _);

    /// <summary>As <see cref="Idle"/>, but yields the minted timers in feed order: CPU, memory,
    /// network.</summary>
    public static SystemMetricsService WithTimers(out List<FakeUiTimer> timers) =>
        WithTimers(out timers, cpu: () => 0);

    /// <summary>The failable form: pass a sampler that throws to drive a feed's failure path.</summary>
    public static SystemMetricsService WithTimers(
        out List<FakeUiTimer> timers,
        Func<double>? cpu = null,
        Func<MemorySample>? memory = null,
        Func<NetworkSample>? network = null,
        Func<string>? adapterName = null) {
        var minted = new List<FakeUiTimer>();
        timers = minted;
        var samplers = new MetricSamplers(
            cpu ?? (() => 0),
            memory ?? (() => new MemorySample(0, 0, 0, 0, 0)),
            network ?? (() => new NetworkSample(0, 0)),
            adapterName ?? (() => "TestNIC"));
        return new SystemMetricsService(samplers, () => {
            var timer = new FakeUiTimer();
            minted.Add(timer);
            return timer;
        });
    }

    /// <summary>Index of each feed's timer in the list <see cref="WithTimers"/> yields, in the order
    /// <see cref="SystemMetricsService"/> constructs them.</summary>
    public const int CpuTimer = 0, MemoryTimer = 1, NetworkTimer = 2;
}
