using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Processes;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

    /// <summary>
    /// Leaving the tab mid-read must abandon the load rather than let it land. Without this the read
    /// completes off-screen and, on a failure, writes the emptying fallback — which the user then meets
    /// as a blank list the next time they open the tab.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WhenTheTabIsLeftMidRead_KeepsTheRowsAlreadyOnScreen() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var provider = new ControllableSnapshotProvider([Proc(100, 0, "editor.exe", ProcessCategory.App)]);
        var viewModel = new ProcessesViewModel(metrics, provider, new FakeProcessInterop());

        // The constructor's own load seeds the list.
        await viewModel.LoadAsync();
        Assert.Single(viewModel.Apps);

        // Now arm the provider to park, then fail — and leave the tab while it is parked.
        provider.Gate = new TaskCompletionSource();
        provider.Fail = new InvalidOperationException("the enumeration broke");
        viewModel.SetActive(true);
        var inFlight = viewModel.LoadAsync();

        viewModel.SetActive(false);   // the user switches away
        provider.Gate.SetResult();    // the read comes back badly, into a page nobody is watching
        await inFlight;

        // The soft-fail would have cleared every group. Off screen, it must not have run.
        Assert.Single(viewModel.Apps);
    }

    /// <summary>
    /// The constructor's own load must survive. It runs before the page is ever shown — universal search
    /// reads the snapshot from tabs the user never opens — so it captures the gate's token while the gate
    /// is still idle. Building the gate after that load would make the capture dereference a null field,
    /// and the soft-fail would swallow it as an ordinary failure and empty the page: a blank tab with
    /// nothing logged. Asserting on the constructor alone, with no explicit load, is what pins the order.
    /// </summary>
    [Fact]
    public void Construction_LoadsTheSnapshotWithoutTrippingOverItsOwnGate() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var provider = new ControllableSnapshotProvider([Proc(100, 0, "editor.exe", ProcessCategory.App)]);

        var viewModel = new ProcessesViewModel(metrics, provider, new FakeProcessInterop());

        Assert.Single(viewModel.Apps);
        Assert.Equal("1", viewModel.TotalProcessesText);
    }

    /// <summary>
    /// A dead CPU counter reports no reading, not a confident 0%. This page used to render the failure as
    /// "0%" while the Dashboard, Performance and Network tabs rendered the very same feed as "—", and
    /// <see cref="MetricChannel{T}"/> stops polling after a failure — so that "0%" sat there claiming an
    /// idle machine for the rest of the session.
    /// </summary>
    [Fact]
    public void CpuSamplerFailure_ReportsNoReadingRatherThanAConfidentZero() {
        var timers = new List<FakeUiTimer>();
        var samplers = new MetricSamplers(
            () => throw new InvalidOperationException("the counter is gone"),
            () => new MemorySample(0, 0, 0, 0, 0),
            () => new NetworkSample(0, 0),
            () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => {
            var timer = new FakeUiTimer();
            timers.Add(timer);
            return timer;
        });
        var viewModel = new ProcessesViewModel(
            metrics, new FakeSnapshotProvider([]), new FakeProcessInterop());

        viewModel.SetActive(true);   // attaches the subscriptions
        timers[0].RaiseTick();       // the CPU feed's timer

        Assert.Equal("—", viewModel.CpuUsageText);
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

    /// <summary>The third group's caption and breakdown word come from <see cref="ProcessGroupNames"/>, so
    /// a Linux desktop reads "System processes" rather than "Windows processes". Asserted against the host's
    /// own arm so the test states the wiring, not the platform.</summary>
    [Fact]
    public async Task LoadAsync_CaptionsTheThirdGroupForThisPlatform() {
        var (viewModel, _) = Create(
            Proc(300, 0, "svchost.exe", ProcessCategory.Windows),
            Proc(301, 0, "lsass.exe", ProcessCategory.Windows));

        await viewModel.LoadAsync();

        Assert.Equal($"{ProcessGroupNames.SystemHeader} · 2", viewModel.WindowsHeader);
        Assert.EndsWith($"2 {ProcessGroupNames.SystemLabel}", viewModel.ProcessBreakdownText, StringComparison.Ordinal);
    }

    /// <summary>The failure path resets to the same caption the constructor used, so a soft-failed poll
    /// cannot leave a Linux desktop showing the Windows wording.</summary>
    [Fact]
    public async Task LoadAsync_EmptySnapshot_ResetsToThePlatformCaption() {
        var (viewModel, _) = Create();

        await viewModel.LoadAsync();

        Assert.StartsWith(ProcessGroupNames.SystemHeader, viewModel.WindowsHeader, StringComparison.Ordinal);
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
        public Task<IReadOnlyList<ProcessInfo>> GetAsync(CancellationToken token = default) => Task.FromResult(processes);
    }

    /// <summary>A provider the test can park and then fail on demand, so a load can be caught in flight
    /// and made to come back badly after the page has gone off screen.</summary>
    private sealed class ControllableSnapshotProvider(IReadOnlyList<ProcessInfo> processes)
        : IProcessSnapshotProvider {
        public TaskCompletionSource? Gate { get; set; }
        public Exception? Fail { get; set; }

        public async Task<IReadOnlyList<ProcessInfo>> GetAsync(CancellationToken token = default) {
            if (Gate is { } gate)
                await gate.Task;
            if (Fail is { } failure)
                throw failure;
            return processes;
        }
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
