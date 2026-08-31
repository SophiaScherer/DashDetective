using DashDetective.Services.SystemMetrics;
using System;
using System.Collections.Generic;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IPhysicalDiskThroughputSampler"/> for headless tests: reports whatever per-disk
/// activity the test stages, so the alert watcher's "worst disk wins" path can be driven without real
/// hardware. The <see cref="FakeGpuUsageSampler"/> shape, including the ability to throw.
/// </summary>
internal sealed class FakeDiskThroughputSampler : IPhysicalDiskThroughputSampler {
    private readonly List<DiskThroughputSample> _samples = [];
    private Exception? _failure;

    /// <summary>Whether <see cref="Dispose"/> has been called — the consumer owns the sampler's lifetime.</summary>
    public bool Disposed { get; private set; }

    /// <summary>Stages a disk reporting an active-time percentage.</summary>
    public FakeDiskThroughputSampler Reporting(int diskNumber, double activePercent) {
        _samples.Add(new DiskThroughputSample(diskNumber, 0, 0, activePercent, 0, 0));
        return this;
    }

    /// <summary>Makes sampling throw, the way a shut PDH query does.</summary>
    public FakeDiskThroughputSampler Throwing(string why = "the query handle is closed") {
        _failure = new InvalidOperationException(why);
        return this;
    }

    public IReadOnlyList<DiskThroughputSample> Sample() {
        if (_failure is { } failure)
            throw failure;

        return Disposed ? [] : _samples;
    }

    public void Dispose() => Disposed = true;
}
