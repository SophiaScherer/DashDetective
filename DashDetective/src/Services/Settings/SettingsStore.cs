using Avalonia.Threading;
using DashDetective.Services.Diagnostics;
using System;
using System.IO;
using System.Text.Json;

namespace DashDetective.Services.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON at <c>%AppData%/DashDetective/settings.json</c>.
/// Pure persistence — it knows nothing about view-models; the composition root applies a loaded
/// snapshot and hands back a fresh one to <see cref="Save"/> whenever a control changes.
///
/// Robustness: <see cref="Load"/> soft-fails to <see cref="AppSettings.Defaults"/> for a missing,
/// corrupt or denied file (never throws), <see cref="Save"/> is debounced (rapid toggling collapses
/// to one disk write) and atomic (write a temp file, then move over the target), and <see cref="Flush"/>
/// forces any pending write on shutdown so a last-moment change isn't lost.
/// </summary>
public sealed class SettingsStore : IDisposable {
    private const int CurrentSchemaVersion = 1;
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);

    private readonly string? _path;
    private readonly DispatcherTimer _saveTimer;
    private AppSettings? _pending;

    public SettingsStore() {
        _path = BuildSettingsPath();
        _saveTimer = new DispatcherTimer { Interval = SaveDebounce };
        _saveTimer.Tick += (_, _) => Flush();
    }

    /// <summary>Reads the persisted settings, or <see cref="AppSettings.Defaults"/> if the file is
    /// missing, unreadable, corrupt, or from a newer/older schema. Never throws.</summary>
    public AppSettings Load() {
        if (_path is null || !File.Exists(_path))
            return AppSettings.Defaults;

        try {
            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);

            // A null (empty/whitespace file) or a schema we don't understand → start clean.
            if (settings is null || settings.SchemaVersion != CurrentSchemaVersion) {
                Log.Warn($"Settings ignored (schema {settings?.SchemaVersion.ToString() ?? "none"}); using defaults");
                return AppSettings.Defaults;
            }

            return settings;
        } catch (Exception e) {
            Log.Warn("Failed to read settings.json; using defaults", e);
            return AppSettings.Defaults;
        }
    }

    /// <summary>Queues <paramref name="settings"/> to be written after a short debounce, coalescing a
    /// burst of changes into a single disk write. The latest snapshot wins.</summary>
    public void Save(AppSettings settings) {
        _pending = settings;
        _saveTimer.Stop();  // restart the window so we only write once the changes settle
        _saveTimer.Start();
    }

    /// <summary>Writes any queued snapshot immediately (e.g. on shutdown) and stops the debounce timer.</summary>
    public void Flush() {
        _saveTimer.Stop();
        if (_pending is not { } settings)
            return;
        _pending = null;
        Write(settings);
    }

    /// <summary>Serializes to a temp file then moves it over the target, so a crash mid-write can't
    /// leave a half-written (corrupt) settings file. Soft-fails with a log line.</summary>
    private void Write(AppSettings settings) {
        if (_path is null)
            return;

        try {
            var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _path, overwrite: true);
        } catch (Exception e) {
            Log.Warn("Failed to write settings.json", e);
        }
    }

    /// <summary>Builds <c>%AppData%/DashDetective/settings.json</c> (Roaming), creating the folder.
    /// Returns <c>null</c> if the folder can't be created, disabling persistence gracefully.</summary>
    private static string? BuildSettingsPath() {
        try {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DashDetective");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        } catch (Exception e) {
            Log.Warn("Could not resolve settings path; persistence disabled", e);
            return null;
        }
    }

    /// <summary>Flushes pending changes and stops the timer.</summary>
    public void Dispose() {
        Flush();
        _saveTimer.Stop();
    }
}
