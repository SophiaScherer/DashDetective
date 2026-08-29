using DashDetective.Shared;
using DashDetective.Shell;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tabs.FileExplorer;
using DashDetective.Tabs.Hardware;
using DashDetective.Tabs.Network;
using DashDetective.Tabs.Performance;
using DashDetective.Tabs.Processes;
using DashDetective.Tabs.Settings;
using DashDetective.Tabs.Storage;
using DashDetective.Tabs.Toolkit;
using System;
using Xunit;

namespace DashDetective.Tests.Shell;

/// <summary>Covers <see cref="RefreshHint"/>, the toolbar's Refresh tooltip. The button is never
/// disabled, so the wording is the only thing telling the user whether a click will do anything.</summary>
public class RefreshHintTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void For_PageThatIgnoresRefresh_SaysSo(bool live) {
        Assert.Equal("Nothing to refresh on this page (F5)", RefreshHint.For(new InertPage(), live));
        Assert.Equal("Nothing to refresh on this page (F5)", RefreshHint.For(null, live));
    }

    [Fact]
    public void For_LiveSamplingPage_WhileLive_SaysTheRefreshIsRedundant() {
        Assert.Equal("Refresh now — this page is already updating live (F5)",
                     RefreshHint.For(new LivePage(), live: true));
    }

    [Fact]
    public void For_LiveSamplingPage_WhilePaused_OffersAPlainRefresh() {
        Assert.Equal("Refresh (F5)", RefreshHint.For(new LivePage(), live: false));
    }

    /// <summary>Hardware and File Explorer re-enumerate only on demand, so Refresh is their sole way to
    /// re-read — the live flag must not change what they are offered.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void For_OnDemandPage_OffersAPlainRefreshWhetherLiveOrNot(bool live) {
        Assert.Equal("Refresh (F5)", RefreshHint.For(new OnDemandPage(), live));
    }

    /// <summary>Pins which arm each real page falls into. Checked on the types, so no page has to be
    /// constructed: a view model that gained or lost <see cref="ILiveSamplingPage"/> would otherwise
    /// change the toolbar's wording with nothing to catch it.</summary>
    [Fact]
    public void EveryPage_LandsInTheExpectedArm() {
        AssertArm<SettingsViewModel>(refreshable: false, liveSampling: false);
        AssertArm<ToolkitViewModel>(refreshable: false, liveSampling: false);

        AssertArm<HardwareViewModel>(refreshable: true, liveSampling: false);
        AssertArm<FileExplorerViewModel>(refreshable: true, liveSampling: false);

        AssertArm<DashboardViewModel>(refreshable: true, liveSampling: true);
        AssertArm<PerformanceViewModel>(refreshable: true, liveSampling: true);
        AssertArm<NetworkViewModel>(refreshable: true, liveSampling: true);
        AssertArm<ProcessesViewModel>(refreshable: true, liveSampling: true);
        AssertArm<StorageViewModel>(refreshable: true, liveSampling: true);
    }

    private static void AssertArm<TPage>(bool refreshable, bool liveSampling) {
        Assert.Equal(refreshable, typeof(IRefreshablePage).IsAssignableFrom(typeof(TPage)));
        Assert.Equal(liveSampling, typeof(ILiveSamplingPage).IsAssignableFrom(typeof(TPage)));
    }

    private sealed class InertPage;

    private sealed class OnDemandPage : IRefreshablePage {
        public void Refresh() { }
    }

    private sealed class LivePage : IRefreshablePage, ILiveSamplingPage {
        public void Refresh() { }

        public void SetLive(bool live) { }
    }
}
