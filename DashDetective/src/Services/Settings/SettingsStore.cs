using Avalonia.Threading;
using DashDetective.Services.Diagnostics;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DashDetective.Services.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON at <c>%AppData%/DashDetective/settings.json</c>
/// (on Linux, <c>$XDG_CONFIG_HOME</c> ?? <c>~/.config</c>).
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

    public SettingsStore()
        : this(BuildSettingsPath()) { }

    /// <summary>Test seam: takes the settings-file path explicitly (production resolves %AppData%). A
    /// null path disables persistence, exactly as an unresolvable folder does.</summary>
    internal SettingsStore(string? path) {
        _path = path;
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
            var settings = Merge(json);

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

    /// <summary>
    /// Deserializes a file over <see cref="AppSettings.Defaults"/>, key by key, rather than on its own.
    ///
    /// <b>This is load-bearing, not belt-and-braces.</b> Deserializing the file directly discards every
    /// non-default initializer on <c>AppSettings</c> for any property the file omits: the source generator
    /// treats a record's <c>init</c> properties as constructor parameters (generated code cannot assign an
    /// <c>init</c> property after construction, so it must use one object initializer), builds the whole
    /// object from a single args array, and fills the absent slots with <c>default(T)</c>. A file written
    /// before a property existed therefore loaded <c>ShowInTray</c> as false and every alert threshold as
    /// 0 — and 0 is how a threshold is switched off.
    ///
    /// Merging as JSON rather than mapping fields keeps that fix general: a property added later inherits
    /// its default from here with no further work. <c>JsonNode</c> is a document API, not reflection-based
    /// serialization, so this stays clean under the trimming/AOT gate.
    /// </summary>
    private static AppSettings? Merge(string json) {
        if (JsonNode.Parse(json) is not JsonObject file)
            return null;   // not an object (empty or hand-mangled) — the caller falls back to defaults

        var merged = JsonSerializer.SerializeToNode(AppSettings.Defaults, SettingsJsonContext.Default.AppSettings)
            as JsonObject;
        if (merged is null)
            return null;

        foreach (var (key, value) in file)
            merged[key] = value?.DeepClone();

        return merged.Deserialize(SettingsJsonContext.Default.AppSettings);
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
    /// <c>ApplicationData</c> resolves to <c>$XDG_CONFIG_HOME</c> ?? <c>~/.config</c> on Linux, a
    /// different tree from <see cref="Log"/>'s, so the two never collide.
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
