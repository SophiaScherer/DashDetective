using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Processes;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessesViewModel"/> through the <see cref="IProcessSnapshotProvider"/>
/// and <see cref="IProcessInterop"/> seams: a canned snapshot builds the tree and summary counts, and the
/// Properties forwarder reaches the interop with the handle the code-behind supplies.</summary>
public class ProcessesViewModelTests {
    private static ProcessInfo Proc(int pid, int parentPid, string name, ProcessCategory category) =>
        new(pid, parentPid, name, "Running", 0, 0, 1, category, 0, 0);

    private static (ProcessesViewModel ViewModel, FakeProcessInterop Interop) Create(
        params ProcessInfo[] processes) {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var interop = new FakeProcessInterop();
        return (new ProcessesViewModel(metrics, new FakeSnapshotProvider(processes), interop), interop);
    }

    /// <summary>A canned snapshot lands in the three Task-Manager-style groups by category.</summary>
    [Fact]
    public async Task LoadAsync_GroupsProcessesByCategory() {
        var (viewModel, _) = Create(
            Proc(100, 0, "editor.exe", ProcessCategory.App),
            Proc(200, 0, "updater.exe", ProcessCategory.Background),
            Proc(300, 0, "svchost.exe", ProcessCategory.Windows));

        await viewModel.LoadAsync();

        Assert.Equal("editor.exe", Assert.Single(viewModel.Apps).Name);
        Assert.Equal("updater.exe", Assert.Single(viewModel.Background).Name);
        Assert.Equal("svchost.exe", Assert.Single(viewModel.WindowsProcesses).Name);
    }

    /// <summary>An empty snapshot clears the groups rather than leaving the previous poll's rows.</summary>
    [Fact]
    public async Task LoadAsync_EmptySnapshot_ClearsEveryGroup() {
        var (viewModel, _) = Create();

        await viewModel.LoadAsync();

        Assert.Empty(viewModel.Apps);
        Assert.Empty(viewModel.Background);
        Assert.Empty(viewModel.WindowsProcesses);
    }

    /// <summary>The dialog needs the owning window handle, which only the view can fetch — so the view
    /// model forwards rather than the code-behind reaching an interop directly.</summary>
    [Fact]
    public void ShowProperties_ForwardsHandleAndPidToTheInterop() {
        var (viewModel, interop) = Create();

        viewModel.ShowProperties(new IntPtr(4242), 1234);

        Assert.Equal((new IntPtr(4242), 1234), Assert.Single(interop.Calls));
    }

    private sealed class FakeSnapshotProvider(IReadOnlyList<ProcessInfo> processes) : IProcessSnapshotProvider {
        public Task<IReadOnlyList<ProcessInfo>> GetAsync() => Task.FromResult(processes);
    }

    private sealed class FakeProcessInterop : IProcessInterop {
        public List<(IntPtr Owner, int Pid)> Calls { get; } = [];
        public bool TryGetIoBytes(Process process, out ulong totalBytes) {
            totalBytes = 0;
            return false;
        }
        public void ShowProperties(IntPtr owner, int pid) => Calls.Add((owner, pid));
    }
}
