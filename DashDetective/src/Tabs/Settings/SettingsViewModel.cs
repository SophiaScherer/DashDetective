using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Diagnostics;
using DashDetective.Services.Settings;
using DashDetective.Services.Startup;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using DashDetective.Shell.Navigation;
using System;
using System.Collections.Generic;
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
    private readonly IStartupRegistration _startup;
    private readonly Func<DiagnosticsFormat, string> _buildReport;
    private readonly Func<string> _buildMetricsCsv;

    // Guards the constructor's initial application of persisted values from raising Changed or writing
    // to the registry (we only react to real user edits after construction).
    private bool _initializing;

    public ObservableCollection<ThemeOption> ThemeOptions { get; }
    public ObservableCollection<AccentOption> AccentOptions { get; }
    public ObservableCollection<ClockFormatOption> ClockFormatOptions { get; }
    public ObservableCollection<IntervalOption> IntervalOptions { get; }

    // ----- Alerts: one row per watched resource, plus how long a breach must last -----

    public AlertThresholdRow CpuAlert { get; }
    public AlertThresholdRow MemoryAlert { get; }
    public AlertThresholdRow GpuAlert { get; }
    public AlertThresholdRow DiskActiveAlert { get; }
    public AlertThresholdRow LowDiskFreeAlert { get; }
    public AlertThresholdRow AlertSustain { get; }

    /// <summary>The shell's navigation bar view-model — the single shared instance, so the Settings
    /// Navigation controls and the on-bar controls stay in sync.</summary>
    public NavigationViewModel Nav { get; }

    /// <summary>The live keyboard bindings — the same instance the shell resolves key presses through, so
    /// this page edits what is actually in force rather than a copy of it.</summary>
    public ShortcutBindings Shortcuts { get; }

    /// <summary>The Keyboard card's rows, grouped by scope exactly as Help lists them.</summary>
    public ObservableCollection<ShortcutRowGroup> ShortcutGroups { get; } = [];

    /// <summary>Whether anything has been rebound, which offers "Restore defaults".</summary>
    public bool HasCustomShortcuts => Shortcuts.HasOverrides;

    /// <summary>Whether a capture box is waiting for a key press. <b>The shell reads this and stands
    /// down while it is set</b>: its listener tunnels from the window, so it sees the key first and would
    /// otherwise run the shortcut being rebound instead of letting it be captured.</summary>
    public bool IsCapturingShortcut { get; private set; }

    /// <summary>Raised after any persisted setting changes (theme, accent, interval, or a toggle), so the
    /// composition root can capture and save the current state.</summary>
    public event Action? Changed;

    /// <summary>The name and description of every setting, which the page's labels bind to. Exposed here
    /// so the copy on screen and the copy universal search matches against are the same strings.</summary>
    public SettingCatalog Catalog => SettingCatalog.Instance;

    /// <summary>Raised when a search result asks for a setting to be shown. UI-only — the view owns
    /// scrolling the row into view and flashing it, the same seam the File Explorer uses for scrolling
    /// and the Processes tab for focus.</summary>
    public event Action<SettingId>? RevealRequested;

    /// <summary>Scrolls a setting into view and flashes it, so a jump from search lands on the row the
    /// user asked for rather than at the top of a page of toggles.</summary>
    public void Reveal(SettingId id) => RevealRequested?.Invoke(id);

    /// <summary>Start DashDetective with Windows (per-user HKCU Run entry).</summary>
    [ObservableProperty] private bool _launchAtStartup;

    /// <summary>Keep running in the tray when the window is closed instead of exiting.</summary>
    [ObservableProperty] private bool _showInTray;

    /// <summary>The master switch for the resource-alert banner. The per-metric thresholds on the Alerts
    /// card are only watched while this is on.</summary>
    [ObservableProperty] private bool _resourceAlerts;

    /// <summary>Read NVIDIA GPU utilization on Linux by running <c>nvidia-smi</c>. Off by default — the
    /// only reading in the app that costs a process launch.</summary>
    [ObservableProperty] private bool _nvidiaGpuMetrics;

    /// <summary>The footer product string, e.g. "DashDetective v0.1.0 · © 2026" — the name and version
    /// come from <see cref="AppInfo"/> (the real assembly metadata), not a hard-coded literal.</summary>
    public string VersionText => $"{AppInfo.Name} v{AppInfo.Version} · © 2026";

    /// <summary>Internal because <see cref="IStartupRegistration"/> is: the class stays public for the
    /// <c>ViewLocator</c> and binding, but the shell is its only caller (and the tests, via
    /// <c>InternalsVisibleTo</c>).</summary>
    internal SettingsViewModel(ThemeService theme, NavigationViewModel nav, SystemMetricsService metrics,
                               AppSettings settings, IStartupRegistration startup,
                               ShortcutBindings shortcuts,
                               Func<DiagnosticsFormat, string> buildReport,
                               Func<string> buildMetricsCsv) {
        _theme = theme;
        _metrics = metrics;
        _startup = startup;
        Nav = nav;
        Shortcuts = shortcuts;
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

        ClockFormatOptions = new ObservableCollection<ClockFormatOption> {
            new("24-hour", ClockFormat.TwentyFourHour, SelectClockFormat),
            new("12-hour", ClockFormat.TwelveHour, SelectClockFormat),
        };

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

        // The shell has already applied the clock format from the same settings, so this only reflects it.
        foreach (var option in ClockFormatOptions)
            option.IsSelected = option.Value == settings.ClockFormat;

        // A usage threshold under 1% would fire constantly and 100 is a real (if rare) ceiling, so the
        // field accepts the whole meaningful span rather than a shortlist. Free space is inverted — it
        // warns when the number drops BELOW — and stops at 99, since "warn while the disk is 100% free"
        // is a warning that never stops.
        CpuAlert = Threshold(settings.AlertCpuEnabled, settings.AlertCpuPercent, 100, "%");
        MemoryAlert = Threshold(settings.AlertMemoryEnabled, settings.AlertMemoryPercent, 100, "%");
        GpuAlert = Threshold(settings.AlertGpuEnabled, settings.AlertGpuPercent, 100, "%");
        DiskActiveAlert = Threshold(settings.AlertDiskActiveEnabled, settings.AlertDiskActivePercent, 100, "%");
        LowDiskFreeAlert = Threshold(settings.AlertLowDiskFreeEnabled, settings.AlertLowDiskFreePercent, 99, "%");

        // The wait is not a warning of its own, so it has no switch — only the seconds. Capped at an hour,
        // past which nothing would ever be reported.
        AlertSustain = new AlertThresholdRow(
            isEnabled: true, settings.AlertSustainSeconds, minimum: 1, maximum: 3600, "s", RaiseChanged);

        // Seed the toggles by assigning the backing fields directly, so the OnChanged hooks don't fire
        // (no spurious registry write / persistence) during construction. Startup reflects the real
        // registry state, which is the ground truth if it was changed outside the app.
        _launchAtStartup = _startup.IsEnabled();
        _showInTray = settings.ShowInTray;
        _resourceAlerts = settings.ResourceAlerts;
        _nvidiaGpuMetrics = settings.NvidiaGpuMetrics;

        // The shell has already loaded any persisted overrides, so this only reflects them. Rebuilt on
        // Changed so a reset — or a rebind from anywhere else — reaches the rows.
        RebuildShortcutRows();
        Shortcuts.Changed += RebuildShortcutRows;

        _initializing = false;
    }

    // ----- Keyboard -----

    /// <summary>Rebuilds the rows from the live bindings, reusing the existing row objects where the
    /// grouping has not changed so a rebind does not blink the whole card away and back.</summary>
    private void RebuildShortcutRows() {
        var groups = Shortcuts.HelpGroups;

        if (ShortcutGroups.Count == groups.Count) {
            for (var i = 0; i < groups.Count; i++)
                UpdateRows(ShortcutGroups[i].Rows, groups[i].Shortcuts);
        } else {
            ShortcutGroups.Clear();
            foreach (var group in groups) {
                var rows = new List<ShortcutRow>(group.Shortcuts.Count);
                foreach (var shortcut in group.Shortcuts)
                    rows.Add(new ShortcutRow(shortcut.Id, shortcut.Description));
                ShortcutGroups.Add(new ShortcutRowGroup(group.Title, rows));
                UpdateRows(rows, group.Shortcuts);
            }
        }

        OnPropertyChanged(nameof(HasCustomShortcuts));
    }

    private void UpdateRows(IReadOnlyList<ShortcutRow> rows, IReadOnlyList<Shortcut> shortcuts) {
        for (var i = 0; i < rows.Count && i < shortcuts.Count; i++) {
            rows[i].Keys = shortcuts[i].Keys;
            rows[i].IsCustom = Shortcuts.IsCustom(shortcuts[i].Id);
        }
    }

    /// <summary>Tracks whether a capture box is armed, for the shell to read.</summary>
    public void SetCapturing(bool capturing) => IsCapturingShortcut = capturing;

    /// <summary>
    /// Applies a captured gesture. A clash inside the same scope is refused and explained on the row —
    /// cross-scope duplicates stay legal, because they already are: Alt+↑ sorts on Processes and climbs a
    /// folder on File Explorer, and only one tab is ever current.
    /// </summary>
    public void Rebind(ShortcutRow row, KeyGesture gesture) {
        ClearNotes();

        if (Shortcuts.TryRebind(row.Id, gesture, out var conflict)) {
            Changed?.Invoke();
            return;
        }

        row.Note = $"{GestureFormatter.Describe(gesture)} is already {DescribeAction(conflict)}.";
    }

    /// <summary>Puts one shortcut back on its shipped binding.</summary>
    public void ResetShortcut(ShortcutRow row) {
        ClearNotes();
        if (!row.IsCustom)
            return;

        Shortcuts.ResetToDefault(row.Id);
        Changed?.Invoke();
    }

    /// <summary>Puts every shortcut back on its shipped binding.</summary>
    [RelayCommand]
    private void ResetAllShortcuts() {
        ClearNotes();
        if (!Shortcuts.HasOverrides)
            return;

        Shortcuts.ResetAll();
        Changed?.Invoke();
    }

    /// <summary>What the conflicting shortcut does, so the refusal names an action rather than an id.</summary>
    private string DescribeAction(ShortcutId id) {
        foreach (var shortcut in Shortcuts.All)
            if (shortcut.Id == id)
                return shortcut.Description.Length > 0 ? $"\"{shortcut.Description}\"" : "in use";

        return "in use";
    }

    /// <summary>Only the newest attempt is explained; leaving older notes up would read as several
    /// things being wrong at once.</summary>
    private void ClearNotes() {
        foreach (var group in ShortcutGroups)
            foreach (var row in group.Rows)
                row.Note = "";
    }

    /// <summary>Whether the "Show in system tray" setting can be operated. Bound to the whole row, not
    /// just the toggle, because a disabled toggle on its own is indistinguishable from an off one.</summary>
    public bool CanUseTray => TrayIntegration.HidesOnClose;

    /// <summary>Whether the "NVIDIA GPU utilization" setting can be operated. Bound to the whole row,
    /// like <see cref="CanUseTray"/>: where the figure needs no helper tool there is nothing to opt into,
    /// and the sampler discards the write anyway.</summary>
    public bool CanUseNvidiaMetrics => GpuMetricsSupport.NeedsHelperTool;

    /// <summary>The currently selected clock format (for capturing into settings). The shell reads this
    /// and pushes it to the toolbar clock and the Toolkit log.</summary>
    public ClockFormat ClockFormat {
        get {
            foreach (var option in ClockFormatOptions)
                if (option.IsSelected)
                    return option.Value;
            return ClockFormat.TwentyFourHour;
        }
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

    /// <summary>Builds the system report in one format (for Copy diagnostics / Export report).</summary>
    public string BuildReport(DiagnosticsFormat format) => _buildReport(format);

    /// <summary>Builds the rolling-history metrics CSV (for Export CSV).</summary>
    public string BuildMetricsCsv() => _buildMetricsCsv();

    private IntervalOption MatchInterval(double seconds) {
        foreach (var option in IntervalOptions)
            if (option.Seconds == seconds)
                return option;
        return IntervalOptions[1]; // default: 1 s
    }

    /// <summary>Flips between light and dark without a trip to this page (Ctrl+Shift+T). Routed through
    /// <see cref="SelectTheme"/> so the segmented control's selection, the <c>Changed</c> event and
    /// persistence all stay in step. Under "System" it flips to whichever explicit theme is the
    /// opposite of what is currently rendering, so the first press always visibly changes something.</summary>
    [RelayCommand]
    private void ToggleTheme() {
        var target = _theme.IsDarkVariantActive ? AppTheme.Light : AppTheme.Dark;
        foreach (var option in ThemeOptions)
            if (option.Value == target) {
                SelectTheme(option);
                return;
            }
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

    /// <summary>Builds one percentage row. Every one shares a floor of 1: zero is how the settings layer
    /// encodes "not watched", and the row's switch says that instead.</summary>
    private AlertThresholdRow Threshold(bool isEnabled, int percent, int maximum, string suffix) =>
        new(isEnabled, percent, minimum: 1, maximum, suffix, RaiseChanged);

    /// <summary>The alert rows' change callback. Guarded like the other seeded controls, though the rows
    /// only report real edits anyway.</summary>
    private void RaiseChanged() {
        if (!_initializing)
            Changed?.Invoke();
    }

    private void SelectClockFormat(ClockFormatOption option) {
        foreach (var other in ClockFormatOptions)
            other.IsSelected = other == option;
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
        _startup.SetEnabled(value);
        Changed?.Invoke();
    }

    partial void OnShowInTrayChanged(bool value) => Changed?.Invoke();

    partial void OnResourceAlertsChanged(bool value) => Changed?.Invoke();

    partial void OnNvidiaGpuMetricsChanged(bool value) => Changed?.Invoke();
}
