using DashDetective.Services.SystemMetrics;
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
}
