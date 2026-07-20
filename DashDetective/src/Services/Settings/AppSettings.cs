using DashDetective.Services.Theming;
using DashDetective.Shell.Navigation;

namespace DashDetective.Services.Settings;

/// <summary>
/// The persisted user preferences, written to <c>settings.json</c> by <see cref="SettingsStore"/>.
/// An immutable snapshot: the composition root applies it on load and captures a fresh one to save
/// whenever a control changes. Every property has a default so a file missing fields (an older
/// schema, a hand-edit) still deserializes; <see cref="SchemaVersion"/> guards future migrations.
/// </summary>
public sealed record AppSettings {
    /// <summary>Bumped when the shape changes incompatibly; a mismatch falls back to <see cref="Defaults"/>.</summary>
    public int SchemaVersion { get; init; } = 1;

    public AppTheme Theme { get; init; } = AppTheme.Dark;

    /// <summary>The chosen accent's <see cref="AccentPreset.Name"/>, or <c>null</c> for the default
    /// multi-colour look.</summary>
    public string? AccentName { get; init; }

    public NavOrientation NavOrientation { get; init; } = NavOrientation.Left;
    public bool NavCollapsed { get; init; }

    /// <summary>Live-metric refresh cadence in seconds (0.5 / 1 / 2 / 5).</summary>
    public double RefreshIntervalSeconds { get; init; } = 1;

    public bool ShowHiddenFiles { get; init; }
    public bool LaunchAtStartup { get; init; }

    /// <summary>Keep running in the tray when the window is closed. On by default (matches the mock).</summary>
    public bool ShowInTray { get; init; } = true;

    /// <summary>Show the in-app banner when CPU or memory stays above the alert threshold.</summary>
    public bool ResourceAlerts { get; init; }

    /// <summary>The first-run baseline, also the soft-fail fallback for a missing/corrupt file. Encodes
    /// the same on/off states the static mock showed, so a fresh install looks unchanged.</summary>
    public static AppSettings Defaults { get; } = new();
}
