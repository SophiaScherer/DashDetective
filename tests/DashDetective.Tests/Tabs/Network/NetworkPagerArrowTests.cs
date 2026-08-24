using DashDetective.Tabs.Network;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Network;

/// <summary>Covers the connections pager's first/prev/next/last arrows. They are stable commands rather
/// than PageLinks entries — that collection is cleared and rebuilt on every 2.5s poll — so what needs
/// pinning is that their enabled state tracks the page, and that the live refresh does not disturb it.</summary>
public class NetworkPagerArrowTests {
    private const int PageSize = 100;

    private sealed class StubAdapters : IAdapterInfoProvider {
        public Task<AdapterSnapshot> GetAsync(CancellationToken token = default) =>
            Task.FromResult(new AdapterSnapshot([], IpConfigInfo.Unknown));
    }

    private sealed class StubDns : IDnsLookupProvider {
        public Task<DnsResult> GetAsync(string host) => Task.FromResult(new DnsResult("", ""));
    }

    /// <summary>Returns the given number of distinct rows, so the page count is <c>rows / PageSize</c>.</summary>
    private sealed class Rows(int count) : IConnectionsProvider {
        public Exception? Fail { get; set; }

        public Task<ConnectionsSnapshot> GetAsync(CancellationToken token = default) {
            if (Fail is { } failure)
                return Task.FromException<ConnectionsSnapshot>(failure);

            var list = new List<ConnectionInfo>(count);
            for (var i = 0; i < count; i++)
                list.Add(new ConnectionInfo("app.exe", $"10.0.0.1:{i}", "1.1.1.1:443", "Established", "TCP", i));
            return Task.FromResult(new ConnectionsSnapshot(list, count));
        }
    }

    private static async Task<NetworkViewModel> LoadedAsync(int rowCount, IConnectionsProvider? provider = null) {
        var vm = new NetworkViewModel(
            new NetworkProviders(new StubAdapters(), provider ?? new Rows(rowCount), new StubDns()));
        vm.Refresh();
        await Task.Yield();
        return vm;
    }

    [Fact]
    public async Task OnASinglePage_NoArrowIsEnabled() {
        var vm = await LoadedAsync(PageSize / 2);

        Assert.False(vm.PagerVisible);
        Assert.False(vm.HasPreviousPage);
        Assert.False(vm.HasNextPage);
    }

    [Fact]
    public async Task OnTheFirstPage_OnlyTheForwardArrowsAreEnabled() {
        var vm = await LoadedAsync(PageSize * 4);

        Assert.True(vm.PagerVisible);
        Assert.False(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);
        Assert.False(vm.FirstPageCommand.CanExecute(null));
        Assert.False(vm.PreviousPageCommand.CanExecute(null));
        Assert.True(vm.NextPageCommand.CanExecute(null));
        Assert.True(vm.LastPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task OnTheLastPage_OnlyTheBackwardArrowsAreEnabled() {
        var vm = await LoadedAsync(PageSize * 4);

        vm.LastPageCommand.Execute(null);

        Assert.True(vm.HasPreviousPage);
        Assert.False(vm.HasNextPage);
        Assert.False(vm.NextPageCommand.CanExecute(null));
        Assert.False(vm.LastPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task InTheMiddle_EveryArrowIsEnabled() {
        var vm = await LoadedAsync(PageSize * 4);

        vm.NextPageCommand.Execute(null);

        Assert.True(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);
    }

    [Fact]
    public async Task LastPage_JumpsToTheEndAndFirstPage_ComesBack() {
        var vm = await LoadedAsync(PageSize * 4);

        vm.LastPageCommand.Execute(null);
        Assert.Contains("page 4 of 4", vm.ConnectionsSummary);

        vm.FirstPageCommand.Execute(null);
        Assert.Contains("page 1 of 4", vm.ConnectionsSummary);
    }

    [Fact]
    public async Task NextAndPrevious_StepOnePageAtATime() {
        var vm = await LoadedAsync(PageSize * 4);

        vm.NextPageCommand.Execute(null);
        vm.NextPageCommand.Execute(null);
        Assert.Contains("page 3 of 4", vm.ConnectionsSummary);

        vm.PreviousPageCommand.Execute(null);
        Assert.Contains("page 2 of 4", vm.ConnectionsSummary);
    }

    /// <summary>Each jump signals the view so it can scroll the list back to the top — the same signal the
    /// numbered links raise, and deliberately not raised by the periodic refresh.</summary>
    [Fact]
    public async Task EachArrow_SignalsThePageChange() {
        var vm = await LoadedAsync(PageSize * 4);
        var raised = 0;
        vm.ConnectionsPageChanged += () => raised++;

        vm.NextPageCommand.Execute(null);
        vm.LastPageCommand.Execute(null);
        vm.PreviousPageCommand.Execute(null);
        vm.FirstPageCommand.Execute(null);

        Assert.Equal(4, raised);
    }

    /// <summary>The live poll rebuilds the page every 2.5s. It must leave the arrows where the user put
    /// them rather than resetting to page one.</summary>
    [Fact]
    public async Task ARefreshAtTheSamePage_LeavesTheArrowsAlone() {
        var vm = await LoadedAsync(PageSize * 4);
        vm.LastPageCommand.Execute(null);

        vm.Refresh();
        await Task.Yield();

        Assert.Contains("page 4 of 4", vm.ConnectionsSummary);
        Assert.True(vm.HasPreviousPage);
        Assert.False(vm.HasNextPage);
    }

    /// <summary>The arrows are stable commands, so unlike PageLinks they survive a clear — an unavailable
    /// list has to disable them by hand or it keeps a live pager over nothing.</summary>
    [Fact]
    public async Task WhenTheConnectionsReadFails_EveryArrowIsDisabled() {
        var rows = new Rows(PageSize * 4);
        var vm = await LoadedAsync(0, rows);
        vm.LastPageCommand.Execute(null);

        rows.Fail = new InvalidOperationException("the TCP table is gone");
        vm.Refresh();
        await Task.Yield();

        Assert.False(vm.PagerVisible);
        Assert.False(vm.HasPreviousPage);
        Assert.False(vm.HasNextPage);
    }
}
