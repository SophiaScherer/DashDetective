using DashDetective.Tabs.Processes;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Pins the counter <see cref="ProcessGpuSampler"/> reads. Its aggregation is
/// <c>GpuEngineLoad.ByProcess</c>, covered by <c>GpuEngineLoadTests</c>.</summary>
public class ProcessGpuSamplerTests {
    /// <summary>Task Manager's source for the per-process GPU column.</summary>
    [Fact]
    public void CounterPath_IsTaskManagersGpuEngineCounter() {
        Assert.Equal(@"\GPU Engine(*)\Utilization Percentage", ProcessGpuSampler.CounterPath);
    }
}
