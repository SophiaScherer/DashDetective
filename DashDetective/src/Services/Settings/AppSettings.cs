using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Shell.Navigation;

namespace DashDetective.Services.Settings;

/// <summary>
/// The persisted user preferences, written to <c>settings.json</c> by <see cref="SettingsStore"/>.
/// An immutable snapshot: the composition root applies it on load and captures a fresh one to save
/// whenever a control changes. Every property has a default so a file missing fields (an older
/// schema, a hand-edit) still deserializes — though the defaults survive that only because
/// <see cref="SettingsStore"/> merges a loaded file over <see cref="Defaults"/>; see the note there
/// before assuming an initializer here is enough. <see cref="SchemaVersion"/> guards future migrations.
/// </summary>
public sealed record AppSettings {
    /// <summary>Bumped when the shape changes incompatibly; a mismatch falls back to <see cref="Defaults"/>.</summary>
    public int SchemaVersion { get; init; } = 1;

    public AppTheme Theme { get; init; } = AppTheme.Dark;

    /// <summary>The chosen accent's <see cref="AccentPreset.Name"/>, or <c>null</c> for the default
    /// multi-colour look.</summary>
    public string? AccentName { get; init; }

    /// <summary>How on-screen wall-clock times read (the toolbar clock, the Toolkit log). Display only:
    /// export file names, the report's "Generated" line and the app log stay 24-hour so files remain
    /// sortable and machine-parseable.</summary>
    public ClockFormat ClockFormat { get; init; } = ClockFormat.TwentyFourHour;

    public NavOrientation NavOrientation { get; init; } = NavOrientation.Left;
    public bool NavCollapsed { get; init; }

    /// <summary>Live-metric refresh cadence in seconds (0.5 / 1 / 2 / 5).</summary>
    public double RefreshIntervalSeconds { get; init; } = 1;

    public bool ShowHiddenFiles { get; init; }
    public bool LaunchAtStartup { get; init; }

    /// <summary>Keep running in the tray when the window is closed. On by default (matches the mock).</summary>
    public bool ShowInTray { get; init; } = true;

    /// <summary>Whether the "still running in the tray" notice has been shown. Not a preference and not on
    /// the Settings page — it is the record that the app has disclosed, once, that closing the window does
    /// not stop it.</summary>
    public bool TrayNoticeShown { get; init; }

    /// <summary>The master switch for the in-app resource-alert banner. The per-metric thresholds below
    /// are only watched while this is on.</summary>
    public bool ResourceAlerts { get; init; }

    /// <summary>Whether each resource is watched. Separate from the threshold beside it so a switched-off
    /// row still remembers its number: GPU and disk activity ship off — sustained saturation of either is
    /// what legitimate heavy work looks like — but with a sensible figure already in the box.</summary>
    public bool AlertCpuEnabled { get; init; } = true;
    public bool AlertMemoryEnabled { get; init; } = true;
    public bool AlertGpuEnabled { get; init; }
    public bool AlertDiskActiveEnabled { get; init; }
    public bool AlertLowDiskFreeEnabled { get; init; } = true;

    /// <summary>Per-metric alert thresholds as percentages, kept whether or not the metric is watched.
    /// The four usage figures are an upper bound; low-disk-space is a lower one.</summary>
    public int AlertCpuPercent { get; init; } = 90;
    public int AlertMemoryPercent { get; init; } = 90;
    public int AlertGpuPercent { get; init; } = 90;
    public int AlertDiskActivePercent { get; init; } = 90;
    public int AlertLowDiskFreePercent { get; init; } = 10;

    /// <summary>How long a usage metric must stay over its threshold before it counts. Seconds rather
    /// than samples, so the wait means the same thing at every refresh interval.</summary>
    public int AlertSustainSeconds { get; init; } = 10;

    /// <summary>Read NVIDIA GPU utilization on Linux by running <c>nvidia-smi</c>. Off by default: it is
    /// the only metric in the app that costs a process launch, and the card reads "—" without it. No
    /// effect on Windows, where the same figure comes from a performance counter.</summary>
    public bool NvidiaGpuMetrics { get; init; }

    /// <summary>Performance tab: show every detected instance in the left rail ("All devices") rather than
    /// only the primary of each kind ("Primary"). Meaningful when a category has more than one instance.</summary>
    public bool PerformanceShowAllDevices { get; init; }

    /// <summary>Performance tab: the CPU resource shows a per-logical-processor chart grid ("Detailed")
    /// rather than the single overall utilization chart.</summary>
    public bool CpuDetailedView { get; init; }

    /// <summary>Performance tab: the GPU resource shows a per-engine chart grid ("Detailed") rather than the
    /// single overall utilization chart.</summary>
    public bool GpuDetailedView { get; init; }

    /// <summary>The last few things opened from the universal search box, newest first, encoded by
    /// <c>RecentSearches</c>. Opaque here, so this record — and the settings file — stay free of any
    /// knowledge of what a search result is. One string rather than a list because this record's
    /// value equality (which the round-trip relies on) compares a collection by reference.</summary>
    public string RecentSearches { get; init; } = "";

    /// <summary>The Toolkit commands the user has pinned, encoded by <c>ToolkitPins</c>. Opaque here for
    /// the same reason as <see cref="RecentSearches"/> above: this record — and the settings file — stay
    /// free of any knowledge of what a Toolkit command is.</summary>
    public string PinnedCommands { get; init; } = "";

    /// <summary>The Toolkit commands the user authored themselves, encoded by <c>ToolkitCommandCodec</c>.
    /// Opaque here for the same reason as the two above. Nothing in this string ever runs on its own: a
    /// stored command becomes a row, and a row runs only when it is clicked.</summary>
    public string CustomCommands { get; init; } = "";

    /// <summary>Each page's widget order, encoded by <c>WidgetOrders</c>. Opaque here for the same
    /// reason as the three above: this record — and the settings file — stay free of any knowledge of
    /// what a widget is.</summary>
    public string WidgetOrders { get; init; } = "";

    /// <summary>The Processes table's column order, encoded by <c>ProcessColumnOrder</c>. Opaque here
    /// for the same reason as the four above: this record — and the settings file — stay free of any
    /// knowledge of what a process column is.</summary>
    public string ProcessColumns { get; init; } = "";

    /// <summary>Processes tab: bring folded sections back after a restart. Off by default — folding a
    /// section is usually a glance, not a preference.</summary>
    public bool ProcessesRememberCollapsed { get; init; }

    /// <summary>Processes tab: bring the sort column and direction back after a restart. Off by
    /// default, for the same reason as <see cref="ProcessesRememberCollapsed"/>.</summary>
    public bool ProcessesRememberSort { get; init; }

    /// <summary>The Processes sections left folded, encoded by <c>EnumListCodec</c>. Written only while
    /// <see cref="ProcessesRememberCollapsed"/> is on. Opaque here, like the four above.</summary>
    public string ProcessesCollapsedSections { get; init; } = "";

    /// <summary>The Processes sort column and direction, encoded by <c>ProcessSortState</c>. Written
    /// only while <see cref="ProcessesRememberSort"/> is on. Opaque here, like the four above.</summary>
    public string ProcessesSort { get; init; } = "";

    /// <summary>The keyboard shortcuts the user has rebound, encoded by <c>ShortcutOverrideCodec</c>.
    /// Opaque here for the same reason as the ones above. Empty means every shortcut is on the binding
    /// it shipped with.</summary>
    public string ShortcutOverrides { get; init; } = "";

    /// <summary>The first-run baseline, also the soft-fail fallback for a missing/corrupt file. Encodes
    /// the same on/off states the static mock showed, so a fresh install looks unchanged.</summary>
    public static AppSettings Defaults { get; } = new();
}
