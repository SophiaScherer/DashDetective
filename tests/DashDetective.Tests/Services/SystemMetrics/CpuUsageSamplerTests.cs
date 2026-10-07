using DashDetective.Services.SystemMetrics;
using System;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers the CPU sampler seam: the idle-based busy-fraction math
/// (<see cref="SystemTimesCpuSampler.ComputeUsage"/>) and <see cref="CpuUsageSampler"/> delegating to
/// whichever <see cref="ICpuSampler"/> it was given (the utility-vs-fallback selection).</summary>
public class CpuUsageSamplerTests {
    [Fact]
    public void ComputeUsage_HalfBusy_ReturnsFifty() {
        // idle = half of total → 50% busy.
        Assert.Equal(50.0, SystemTimesCpuSampler.ComputeUsage(idleDelta: 500, totalDelta: 1000));
    }

    [Fact]
    public void ComputeUsage_FullyBusy_ReturnsHundred() {
        Assert.Equal(100.0, SystemTimesCpuSampler.ComputeUsage(idleDelta: 0, totalDelta: 1000));
    }

    [Fact]
    public void ComputeUsage_FullyIdle_ReturnsZero() {
        Assert.Equal(0.0, SystemTimesCpuSampler.ComputeUsage(idleDelta: 1000, totalDelta: 1000));
    }

    [Fact]
    public void ComputeUsage_EmptyInterval_ReturnsZero() {
        // No elapsed processor time → nothing to divide by.
        Assert.Equal(0.0, SystemTimesCpuSampler.ComputeUsage(idleDelta: 0, totalDelta: 0));
    }

    [Fact]
    public void ComputeUsage_PartialLoad_ReturnsBusyPercent() {
        // idle = 3/4 of total → 25% busy.
        Assert.Equal(25.0, SystemTimesCpuSampler.ComputeUsage(idleDelta: 750, totalDelta: 1000));
    }

    [Fact]
    public void Sample_DelegatesToInjectedSampler() {
        using var sampler = new CpuUsageSampler(new StubCpuSampler(73.5));
        Assert.Equal(73.5, sampler.Sample());
    }

    /// <summary>The public constructor is the one place the platform is chosen. Asserts the arm rather
    /// than a reading, so it holds on both CI legs — a host with no usable counters still reads 0.</summary>
    [Fact]
    public void PublicConstructor_PicksThisPlatformsReader() {
        using var sampler = new CpuUsageSampler();

        var expected = OperatingSystem.IsLinux() ? typeof(LinuxCpuSampler)
            : OperatingSystem.IsWindows() ? null                     // either PDH arm, depending on the host
            : typeof(UnsupportedCpuSampler);

        if (expected is not null)
            Assert.IsType(expected, sampler.Inner);
        else
            Assert.True(sampler.Inner is ProcessorUtilityCpuSampler or SystemTimesCpuSampler);
    }

    [Fact]
    public void UtilityIfReady_Ready_KeepsTheUtilityCounter() {
        var utility = new StubCpuSampler(10);

        Assert.Same(utility, CpuUsageSampler.UtilityIfReady(utility, ready: true));
        Assert.False(utility.Disposed);
    }

    /// <summary>A counter that did not stand up is released and the caller takes the fallback.</summary>
    [Fact]
    public void UtilityIfReady_NotReady_DisposesItAndFallsBack() {
        var utility = new StubCpuSampler(10);

        Assert.Null(CpuUsageSampler.UtilityIfReady(utility, ready: false));
        Assert.True(utility.Disposed);
    }

    /// <summary>On Windows the public constructor keeps the utility counter whenever it stands up on this
    /// host, and only otherwise reads <c>GetSystemTimes</c>.</summary>
    [Fact]
    public void PublicConstructor_OnWindows_PrefersTheUtilityCounterWhenItStandsUp() {
        if (!OperatingSystem.IsWindows())
            return;

        bool ready;
        using (var probe = new ProcessorUtilityCpuSampler())
            ready = probe.Ready;
        using var sampler = new CpuUsageSampler();

        Assert.IsType(ready ? typeof(ProcessorUtilityCpuSampler) : typeof(SystemTimesCpuSampler), sampler.Inner);
    }

    /// <summary>Zero is the no-data contract every arm shares, so the Dashboard degrades to a flat line
    /// rather than failing on a platform whose milestone has not landed.</summary>
    [Fact]
    public void UnsupportedCpuSampler_SamplesZero() {
        Assert.Equal(0.0, new UnsupportedCpuSampler().Sample());
    }

    /// <summary>Fake <see cref="ICpuSampler"/> — stands in for the utility counter or the fallback so the
    /// coordinator's delegation can be verified without real hardware counters.</summary>
    private sealed class StubCpuSampler(double value) : ICpuSampler, IDisposable {
        public bool Disposed { get; private set; }

        public double Sample() => value;

        public void Dispose() => Disposed = true;
    }
}
