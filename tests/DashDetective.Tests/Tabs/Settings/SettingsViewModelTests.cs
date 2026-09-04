using Avalonia.Input;
using DashDetective.Services.Accessibility;
using DashDetective.Services.Network;
using DashDetective.Services.Notifications;
using DashDetective.Services.Settings;
using DashDetective.Services.Startup;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using DashDetective.Shell.Navigation;
using DashDetective.Tabs.Settings;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>Covers <see cref="SettingsViewModel"/>'s startup toggle through the
/// <see cref="IStartupRegistration"/> seam: it is seeded from the real registration, construction
/// itself must never write, and a user edit writes through exactly once.</summary>
public class SettingsViewModelTests {
    private static SettingsViewModel Create(IStartupRegistration startup) =>
        Create(startup, () => { });

    private static SettingsViewModel Create(IStartupRegistration startup, Action resetWidgetOrders,
                                            AppSettings? settings = null) {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        var theme = new ThemeService();
        return new SettingsViewModel(
            theme, new AccessibilityService(theme), new NavigationViewModel(), metrics,
            settings ?? AppSettings.Defaults, startup,
            new ShortcutBindings(), _ => "", () => "", resetWidgetOrders);
    }

    [Fact]
    public void Ctor_SeedsLaunchAtStartupFromRegistration() {
        Assert.True(Create(new FakeStartupRegistration(enabled: true)).LaunchAtStartup);
        Assert.False(Create(new FakeStartupRegistration(enabled: false)).LaunchAtStartup);
    }

    /// <summary>The toggles are seeded by assigning the backing fields, precisely so the OnChanged hook
    /// doesn't fire a registry write while the page is being built. Pinned here so nobody "tidies" the
    /// field assignment into a property assignment.</summary>
    [Fact]
    public void Ctor_DoesNotWriteToRegistration() {
        var startup = new FakeStartupRegistration(enabled: true);

        Create(startup);

        Assert.Empty(startup.Writes);
    }

    [Fact]
    public void LaunchAtStartup_UserEdit_WritesThroughOnce() {
        var startup = new FakeStartupRegistration(enabled: false);
        var viewModel = Create(startup);

        viewModel.LaunchAtStartup = true;

        Assert.Equal(new[] { true }, startup.Writes);
    }

    /// <summary>The orders live on the shell, not here, so the button is a forwarder and the test is
    /// that it forwards. Resetting is idempotent, so nothing gates the command.</summary>
    [Fact]
    public void ResetWidgetPlacements_InvokesTheShellsResetAction() {
        var resets = 0;
        var viewModel = Create(new FakeStartupRegistration(enabled: false), () => resets++);

        viewModel.ResetWidgetPlacementsCommand.Execute(null);
        viewModel.ResetWidgetPlacementsCommand.Execute(null);

        Assert.Equal(2, resets);
    }

    /// <summary>The reset happens on pages the user is not looking at, so it has to say it happened.</summary>
    [Fact]
    public void ResetWidgetPlacements_Confirms() {
        var viewModel = Create(new FakeStartupRegistration(enabled: false), () => { });
        var notices = new List<string>();
        viewModel.Notify = notices.Add;

        viewModel.ResetWidgetPlacementsCommand.Execute(null);

        Assert.Equal([Notices.WidgetPlacementsReset], notices);
    }

    /// <summary>Nothing was rebound, so the command returns early and there is nothing to confirm. A
    /// banner here would claim an undo that never happened.</summary>
    [Fact]
    public void ResetAllShortcuts_NothingRebound_StaysSilent() {
        var viewModel = Create(new FakeStartupRegistration(enabled: false));
        var notices = new List<string>();
        viewModel.Notify = notices.Add;

        viewModel.ResetAllShortcutsCommand.Execute(null);

        Assert.Empty(notices);
    }

    [Fact]
    public void ResetAllShortcuts_AfterARebind_Confirms() {
        var viewModel = Create(new FakeStartupRegistration(enabled: false));
        Assert.True(viewModel.Shortcuts.TryRebind(ShortcutId.Refresh, new KeyGesture(Key.F8), out _));
        var notices = new List<string>();
        viewModel.Notify = notices.Add;

        viewModel.ResetAllShortcutsCommand.Execute(null);

        Assert.Equal([Notices.ShortcutsRestored], notices);
    }

    /// <summary>A folded card's body is never measured, so its rows are not in the visual tree for the
    /// view to scroll to. The jump has to open the card before it asks.</summary>
    [Fact]
    public void Reveal_ExpandsTheCardTheSettingSitsOn() {
        var viewModel = Create(new FakeStartupRegistration(enabled: false));
        viewModel.Collapse.Set("settings.alerts", collapsed: true);
        var revealed = new List<SettingId>();
        viewModel.RevealRequested += revealed.Add;

        viewModel.Reveal(SettingId.AlertCpu);

        Assert.False(viewModel.Collapse.IsCollapsed("settings.alerts"));
        Assert.Equal([SettingId.AlertCpu], revealed);
    }

    [Fact]
    public void Reveal_LeavesEveryOtherCardFolded() {
        var viewModel = Create(new FakeStartupRegistration(enabled: false));
        viewModel.Collapse.Set("settings.keyboard", collapsed: true);

        viewModel.Reveal(SettingId.Theme);

        Assert.True(viewModel.Collapse.IsCollapsed("settings.keyboard"));
    }

    [Fact]
    public void Ctor_SeedsCollapseFromSettings() {
        var settings = AppSettings.Defaults with { CollapsedWidgets = "settings.keyboard" };

        var viewModel = Create(new FakeStartupRegistration(enabled: false), () => { }, settings);

        Assert.True(viewModel.Collapse.IsCollapsed("settings.keyboard"));
    }

    /// <summary>Seeding is a restore, not an edit — the shell must not save back what it just read.</summary>
    [Fact]
    public void Ctor_SeedingCollapse_RaisesNoChange() {
        var settings = AppSettings.Defaults with { CollapsedWidgets = "settings.keyboard" };
        var changes = 0;

        var viewModel = Create(new FakeStartupRegistration(enabled: false), () => { }, settings);
        viewModel.Changed += () => changes++;

        Assert.Equal(0, changes);
    }

    [Fact]
    public void Collapse_RaisesChangedSoTheShellPersists() {
        var viewModel = Create(new FakeStartupRegistration(enabled: false));
        var changes = 0;
        viewModel.Changed += () => changes++;

        viewModel.Collapse.Set("settings.alerts", collapsed: true);

        Assert.Equal(1, changes);
    }

    /// <summary>The tray toggle is shown disabled rather than removed where there is no tray to hide
    /// into, so the row still explains itself and still turns up in search.</summary>
    [Fact]
    public void CanUseTray_FollowsWhetherTheDesktopHasATray() =>
        Assert.Equal(TrayIntegration.HidesOnClose,
                     Create(new FakeStartupRegistration(enabled: false)).CanUseTray);

    /// <summary>Likewise the NVIDIA toggle, which is inert where the figure needs no helper tool: the
    /// sampler discards the write there, so an operable row would be a control that does nothing.</summary>
    [Fact]
    public void CanUseNvidiaMetrics_FollowsWhetherTheFigureNeedsAHelperTool() =>
        Assert.Equal(GpuMetricsSupport.NeedsHelperTool,
                     Create(new FakeStartupRegistration(enabled: false)).CanUseNvidiaMetrics);

    private sealed class FakeStartupRegistration(bool enabled) : IStartupRegistration {
        public List<bool> Writes { get; } = [];
        public bool IsEnabled() => enabled;
        public void SetEnabled(bool value) => Writes.Add(value);
    }
}
