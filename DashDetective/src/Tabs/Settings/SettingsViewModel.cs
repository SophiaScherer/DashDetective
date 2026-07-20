using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Services.Settings;
using DashDetective.Services.Startup;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Shell.Navigation;
using System;
using System.Collections.ObjectModel;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// Backs the whole Settings page. Owns the Appearance option lists (applied through the shared
/// <see cref="ThemeService"/> — the single theming seam), the Monitoring controls (refresh interval,
/// resource-alert / tray / startup toggles) and the Export &amp; Data actions. It exposes the shared
/// <see cref="NavigationViewModel"/> so the Navigation controls drive the same bar as the on-bar
/// controls. It raises <see cref="Changed"/> after any persisted control changes; the composition root
/// observes that (and the Nav / File-Explorer state) to write settings to disk.
/// </summary>
public partial class SettingsViewModel : ViewModelBase {
    private readonly ThemeService _theme;
    private readonly SystemMetricsService _metrics;
    private readonly Func<string> _buildReport;
    private readonly Func<string> _buildMetricsCsv;

    // Guards the constructor's initial application of persisted values from raising Changed or writing
    // to the registry (we only react to real user edits after construction).
    private bool _initializing;

    public ObservableCollection<ThemeOption> ThemeOptions { get; }
    public ObservableCollection<AccentOption> AccentOptions { get; }
    public ObservableCollection<IntervalOption> IntervalOptions { get; }

    /// <summary>The shell's navigation bar view-model — the single shared instance, so the Settings
    /// Navigation controls and the on-bar controls stay in sync.</summary>
    public NavigationViewModel Nav { get; }

    /// <summary>Raised after any persisted setting changes (theme, accent, interval, or a toggle), so the
    /// composition root can capture and save the current state.</summary>
    public event Action? Changed;

    /// <summary>Start DashDetective with Windows (per-user HKCU Run entry).</summary>
    [ObservableProperty] private bool _launchAtStartup;

    /// <summary>Keep running in the tray when the window is closed instead of exiting.</summary>
    [ObservableProperty] private bool _showInTray;

    /// <summary>Show the in-app banner when CPU or memory stays above the alert threshold.</summary>
    [ObservableProperty] private bool _resourceAlerts;

    /// <summary>The footer product string, e.g. "DashDetective v0.1.0 · © 2026" — the name and version
    /// come from <see cref="AppInfo"/> (the real assembly metadata), not a hard-coded literal.</summary>
    public string VersionText => $"{AppInfo.Name} v{AppInfo.Version} · © 2026";

    public SettingsViewModel(ThemeService theme, NavigationViewModel nav, SystemMetricsService metrics,
                             AppSettings settings, Func<string> buildReport, Func<string> buildMetricsCsv) {
        _theme = theme;
        _metrics = metrics;
        Nav = nav;
        _buildReport = buildReport;
        _buildMetricsCsv = buildMetricsCsv;
        _initializing = true;

        ThemeOptions = new ObservableCollection<ThemeOption> {
            new("Dark", AppTheme.Dark, SelectTheme),
            new("Light", AppTheme.Light, SelectTheme),
            new("System", AppTheme.System, SelectTheme),
        };

        // The default (multi-colour) option comes first, then the single accents.
        AccentOptions = new ObservableCollection<AccentOption> {
            new(null, SelectAccent),
        };
        foreach (var preset in AccentPreset.All)
            AccentOptions.Add(new AccentOption(preset, SelectAccent));

        IntervalOptions = new ObservableCollection<IntervalOption> {
            new("0.5s", 0.5, SelectInterval),
            new("1s", 1, SelectInterval),
            new("2s", 2, SelectInterval),
            new("5s", 5, SelectInterval),
        };

        // Reflect the theme service's current selections (the shell already applied them from settings).
        foreach (var option in ThemeOptions)
            option.IsSelected = option.Value == _theme.CurrentTheme;
        foreach (var option in AccentOptions)
            option.IsSelected = Equals(option.Preset, _theme.CurrentAccent);

        // Select and apply the persisted refresh interval (falling back to 1 s if it's an unknown value).
        var interval = MatchInterval(settings.RefreshIntervalSeconds);
        SelectInterval(interval);

        // Seed the toggles by assigning the backing fields directly, so the OnChanged hooks don't fire
        // (no spurious registry write / persistence) during construction. Startup reflects the real
        // registry state, which is the ground truth if it was changed outside the app.
        _launchAtStartup = StartupRegistration.IsEnabled();
        _showInTray = settings.ShowInTray;
        _resourceAlerts = settings.ResourceAlerts;

        _initializing = false;
    }

    /// <summary>The currently selected refresh interval in seconds (for capturing into settings).</summary>
    public double SelectedIntervalSeconds {
        get {
            foreach (var option in IntervalOptions)
                if (option.IsSelected)
                    return option.Seconds;
            return 1;
        }
    }

    /// <summary>Builds the plain-text system report (for Copy diagnostics / Export report).</summary>
    public string BuildReport() => _buildReport();

    /// <summary>Builds the rolling-history metrics CSV (for Export CSV).</summary>
    public string BuildMetricsCsv() => _buildMetricsCsv();

    private IntervalOption MatchInterval(double seconds) {
        foreach (var option in IntervalOptions)
            if (option.Seconds == seconds)
                return option;
        return IntervalOptions[1]; // default: 1 s
    }

    private void SelectTheme(ThemeOption option) {
        foreach (var other in ThemeOptions)
            other.IsSelected = other == option;
        _theme.ApplyTheme(option.Value);
        Changed?.Invoke();
    }

    private void SelectAccent(AccentOption option) {
        foreach (var other in AccentOptions)
            other.IsSelected = other == option;

        if (option.Preset is { } preset)
            _theme.ApplyAccent(preset);
        else
            _theme.ApplyDefaultAppearance();
        Changed?.Invoke();
    }

    private void SelectInterval(IntervalOption option) {
        foreach (var other in IntervalOptions)
            other.IsSelected = other == option;
        _metrics.SetInterval(TimeSpan.FromSeconds(option.Seconds));
        if (!_initializing)
            Changed?.Invoke();
    }

    partial void OnLaunchAtStartupChanged(bool value) {
        StartupRegistration.SetEnabled(value);
        Changed?.Invoke();
    }

    partial void OnShowInTrayChanged(bool value) => Changed?.Invoke();

    partial void OnResourceAlertsChanged(bool value) => Changed?.Invoke();
}
