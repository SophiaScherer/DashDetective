using DashDetective.Services.SystemMetrics;
using System;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Pins the counter <see cref="ProcessorUtilityCpuSampler"/> reads, so the CPU headline cannot be
/// moved off Task Manager's source without a failing test.</summary>
public class ProcessorUtilityCpuSamplerTests {
    /// <summary>Task Manager's source for the CPU headline.</summary>
    [Fact]
    public void CounterPath_IsTaskManagersTotalUtilityCounter() {
        Assert.Equal(@"\Processor Information(_Total)\% Processor Utility", ProcessorUtilityCpuSampler.CounterPath);
    }

    /// <summary>A failed read on a ready sampler must throw, or <c>MetricChannel</c> never shows the placeholder.</summary>
    [Theory]
    [InlineData(1u, 0u)]
    [InlineData(0u, 1u)]
    public void Interpret_FailedRead_Throws(uint collect, uint format) {
        Assert.Throws<InvalidOperationException>(() => ProcessorUtilityCpuSampler.Interpret(collect, format, 50));
    }

    [Theory]
    [InlineData(42.5, 42.5)]
    [InlineData(-3, 0)]
    [InlineData(180, 100)]
    public void Interpret_SuccessfulRead_ClampsToPercent(double raw, double expected) {
        Assert.Equal(expected, ProcessorUtilityCpuSampler.Interpret(0, 0, raw));
    }

    /// <summary>The inert-at-start zero contract is unchanged: no read is attempted, nothing throws.</summary>
    [Fact]
    public void Sample_InertAtStart_StillReturnsZero() {
        Assert.Equal(0, new ProcessorUtilityCpuSampler(SamplerInit.Inert).Sample());
    }
}
