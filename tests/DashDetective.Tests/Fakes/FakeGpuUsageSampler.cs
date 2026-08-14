using DashDetective.Services.SystemMetrics;
using System.Collections.Generic;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IGpuUsageSampler"/> for headless tests: reports whatever readings the test
/// stages, so the view models' "this adapter reports nothing" path can be driven without a GPU. That path
/// is otherwise unreachable in a test, since each page resolves its own sampler.
/// </summary>
internal sealed class FakeGpuUsageSampler : IGpuUsageSampler {
    private readonly Dictionary<string, GpuAdapterSample> _samples = [];

    public bool NvidiaMetricsEnabled { get; set; }

    /// <summary>Whether <see cref="Dispose"/> has been called — the page owns the sampler's lifetime.</summary>
    public bool Disposed { get; private set; }

    /// <summary>How many times readings were asked for, so a test can tell a sampler that was never
    /// consulted from one that was consulted and had nothing to say.</summary>
    public int SampleCount { get; private set; }

    /// <summary>Stages an adapter reporting a utilisation figure.</summary>
    public FakeGpuUsageSampler Reporting(string adapterKey, double overall) {
        _samples[adapterKey] = new GpuAdapterSample(overall, new Dictionary<string, double>());
        return this;
    }

    /// <summary>Stages an adapter that exists but cannot report one — the NVIDIA/Intel case on Linux.</summary>
    public FakeGpuUsageSampler Silent(string adapterKey) {
        _samples[adapterKey] = new GpuAdapterSample(null, new Dictionary<string, double>());
        return this;
    }

    /// <summary>Reports nothing once disposed, matching the real samplers. A fake that kept answering after
    /// its close is what hid the bug where the inventory load disposed a live page's sampler: the PDH query
    /// was shut and every GPU readout went dead, and no test could see it.</summary>
    public IReadOnlyDictionary<string, GpuAdapterSample> SampleAdapters() {
        SampleCount++;
        return Disposed ? new Dictionary<string, GpuAdapterSample>() : _samples;
    }

    public void Dispose() => Disposed = true;
}
