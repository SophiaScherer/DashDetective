using DashDetective.Services.Network;
using DashDetective.Services.Settings;
using DashDetective.Services.Startup;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Shell.Navigation;
using DashDetective.Tabs.Settings;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>Covers <see cref="SettingsViewModel"/>'s startup toggle through the
/// <see cref="IStartupRegistration"/> seam: it is seeded from the real registration, construction
/// itself must never write, and a user edit writes through exactly once.</summary>
public class SettingsViewModelTests {
    private static SettingsViewModel Create(IStartupRegistration startup) {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var metrics = new SystemMetricsService(samplers, () => new FakeUiTimer());
        return new SettingsViewModel(
            new ThemeService(), new NavigationViewModel(), metrics, AppSettings.Defaults, startup,
            () => "", () => "");
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

    /// <summary>The tray toggle is shown disabled rather than removed where there is no tray to hide
    /// into, so the row still explains itself and still turns up in search.</summary>
    [Fact]
    public void CanUseTray_FollowsWhetherTheDesktopHasATray() =>
        Assert.Equal(TrayIntegration.HidesOnClose,
                     Create(new FakeStartupRegistration(enabled: false)).CanUseTray);

    private sealed class FakeStartupRegistration(bool enabled) : IStartupRegistration {
        public List<bool> Writes { get; } = [];
        public bool IsEnabled() => enabled;
        public void SetEnabled(bool value) => Writes.Add(value);
    }
}
