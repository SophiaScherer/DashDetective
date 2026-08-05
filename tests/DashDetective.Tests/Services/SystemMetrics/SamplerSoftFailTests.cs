using DashDetective.Services.SystemMetrics;
using System;
using System.ComponentModel;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>
/// Pins the native samplers' inert soft-fail contract: when the underlying library can't be loaded or
/// bound, each sampler returns its zero/empty reading forever rather than throwing out of a caller's
/// field initialiser.
///
/// This is the behaviour most likely to rot. On a healthy Windows box — and on the Windows CI runner —
/// the real constructors always succeed, so nothing else in the suite ever executes these paths. Each
/// sampler carries an internal <see cref="SamplerInit.Inert"/> constructor purely so this file can reach
/// them.
/// </summary>
public class SamplerSoftFailTests {
    [Fact]
    public void NativeLoadFailure_MatchesTheThreeNativeBindingFailures() {
        Assert.True(NativeLoadFailure.Matches(new DllNotFoundException()));
        Assert.True(NativeLoadFailure.Matches(new EntryPointNotFoundException()));
        Assert.True(NativeLoadFailure.Matches(new BadImageFormatException()));
    }

    /// <summary>The filter has to stay narrow — a genuine Win32 or logic bug must still surface rather
    /// than being swallowed by a sampler constructor. <see cref="TypeLoadException"/> is the base of the
    /// first two and is deliberately not matched.</summary>
    [Fact]
    public void NativeLoadFailure_DoesNotMatchOrdinaryExceptions() {
        Assert.False(NativeLoadFailure.Matches(new TypeLoadException()));
        Assert.False(NativeLoadFailure.Matches(new Win32Exception(5)));
        Assert.False(NativeLoadFailure.Matches(new InvalidOperationException()));
        Assert.False(NativeLoadFailure.Matches(new ArgumentNullException()));
        Assert.False(NativeLoadFailure.Matches(new OutOfMemoryException()));
    }

    [Fact]
    public void ProcessorUtilityCpuSampler_Inert_IsNotReadyAndSamplesZero() {
        using var sampler = new ProcessorUtilityCpuSampler(SamplerInit.Inert);

        Assert.False(sampler.Ready);
        Assert.Equal(0.0, sampler.Sample());
        Assert.Equal(0.0, sampler.Sample());   // repeatable: the flag holds, nothing re-enters PDH
        sampler.Dispose();                     // the `using` disposes again — must be safe twice
    }

    [Fact]
    public void SystemTimesCpuSampler_Inert_IsNotReadyAndSamplesZero() {
        var sampler = new SystemTimesCpuSampler(SamplerInit.Inert);

        Assert.False(sampler.Ready);
        Assert.Equal(0.0, sampler.Sample());
        Assert.Equal(0.0, sampler.Sample());
    }

    /// <summary>The coordinator when even the fallback is inert — the both-CPU-paths-failed case. It must
    /// read 0 rather than throw, which is what keeps the Dashboard up on a host with no usable counters.
    /// </summary>
    [Fact]
    public void CpuUsageSampler_OverAnInertFallback_SamplesZero() {
        using var sampler = new CpuUsageSampler(new SystemTimesCpuSampler(SamplerInit.Inert));

        Assert.Equal(0.0, sampler.Sample());
        Assert.Equal(0.0, sampler.Sample());
    }

    [Fact]
    public void GpuUsageSampler_Inert_SamplesZeroAndEmptyMaps() {
        using var sampler = new GpuUsageSampler(SamplerInit.Inert);

        Assert.Equal(0.0, sampler.Sample());
        Assert.Empty(sampler.SampleEngines());
        Assert.Empty(sampler.SampleAdapters());
        Assert.Equal(0.0, sampler.Sample());
        sampler.Dispose();
    }

    [Fact]
    public void PhysicalDiskThroughputSampler_Inert_SamplesEmpty() {
        using var sampler = new PhysicalDiskThroughputSampler(SamplerInit.Inert);

        Assert.Empty(sampler.Sample());
        Assert.Empty(sampler.Sample());
        sampler.Dispose();
    }

    [Fact]
    public void ProcessorFrequencySampler_Inert_SamplesZero() {
        using var sampler = new ProcessorFrequencySampler(SamplerInit.Inert);

        Assert.Equal(0.0, sampler.Sample());
        Assert.Equal(0.0, sampler.Sample());
        sampler.Dispose();
    }

    [Fact]
    public void LogicalProcessorSampler_Inert_SamplesEmpty() {
        using var sampler = new LogicalProcessorSampler(SamplerInit.Inert);

        Assert.Empty(sampler.Sample());
        Assert.Empty(sampler.Sample());
        sampler.Dispose();
    }

    /// <summary>Memory latches inert in <c>Sample()</c> rather than a constructor, so the repeat call also
    /// proves the latch stops the native call being re-entered.</summary>
    [Fact]
    public void MemoryUsageSampler_Inert_ReturnsAZeroedReading() {
        var sampler = new MemoryUsageSampler(SamplerInit.Inert);

        Assert.Equal(new MemorySample(0, 0, 0, 0, 0), sampler.Sample());
        Assert.Equal(new MemorySample(0, 0, 0, 0, 0), sampler.Sample());
    }

    /// <summary>
    /// The whole point of the milestone: constructing and sampling every native sampler must not throw on
    /// any host. On Windows the counters stand up; anywhere else the guards make these go inert. Asserts
    /// no values — only that nothing escapes — so it stays true on both CI legs. <see cref="CpuUsageSampler"/>
    /// belongs here rather than in the Windows-only fact below because its constructor now picks the
    /// platform's reader, so it is the one CPU entry point that is genuinely callable everywhere.
    /// </summary>
    [Fact]
    public void RealConstructorsAndSamples_NeverThrow_OnThisHost() {
        using var cpu = new CpuUsageSampler();
        using var gpu = new GpuUsageSampler();
        using var disk = new PhysicalDiskThroughputSampler();
        using var frequency = new ProcessorFrequencySampler();
        using var logical = new LogicalProcessorSampler();

        _ = cpu.Sample();
        _ = gpu.Sample();
        _ = gpu.SampleEngines();
        _ = gpu.SampleAdapters();
        _ = disk.Sample();
        _ = frequency.Sample();
        _ = logical.Sample();
        _ = new MemoryUsageSampler().Sample();
    }

    /// <summary>The same contract for the two PDH/kernel32 CPU readers, which now carry
    /// <c>[SupportedOSPlatform("windows")]</c> on their constructors — so the call has to sit behind a
    /// guard, and the fact simply does not run on the Linux leg.</summary>
    [Fact]
    public void RealWindowsCpuConstructorsAndSamples_NeverThrow() {
        if (!OperatingSystem.IsWindows())
            return;

        using var utility = new ProcessorUtilityCpuSampler();

        _ = utility.Sample();
        _ = new SystemTimesCpuSampler().Sample();
    }
}
