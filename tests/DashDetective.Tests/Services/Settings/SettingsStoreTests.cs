using DashDetective.Services.Settings;
using DashDetective.Services.Theming;
using System;
using System.IO;
using Xunit;

namespace DashDetective.Tests.Services.Settings;

/// <summary>Covers <see cref="SettingsStore"/> against a temp-directory path (via the internal path
/// seam): the save/load round-trip, soft-fail to defaults on a missing/corrupt/wrong-schema file, the
/// atomic write leaving no temp file, and the disabled-persistence (null path) case.</summary>
public sealed class SettingsStoreTests : IDisposable {
    private readonly string _dir;
    private readonly string _path;

    public SettingsStoreTests() {
        _dir = Path.Combine(Path.GetTempPath(), "DashDetectiveTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "settings.json");
    }

    public void Dispose() {
        try {
            Directory.Delete(_dir, recursive: true);
        } catch {
            // Best-effort cleanup of the temp directory.
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsSettings() {
        var settings = AppSettings.Defaults with {
            Theme = AppTheme.Light,
            AccentName = "Teal",
            RefreshIntervalSeconds = 2,
            LaunchAtStartup = true,
        };

        using (var store = new SettingsStore(_path)) {
            store.Save(settings);
            store.Flush();
        }

        var loaded = new SettingsStore(_path).Load();
        Assert.Equal(settings, loaded);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults() {
        Assert.Equal(AppSettings.Defaults, new SettingsStore(_path).Load());
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaultsWithoutThrowing() {
        File.WriteAllText(_path, "{ this is not valid json ");
        Assert.Equal(AppSettings.Defaults, new SettingsStore(_path).Load());
    }

    [Fact]
    public void Load_SchemaMismatch_ReturnsDefaults() {
        File.WriteAllText(_path, "{ \"SchemaVersion\": 999 }");
        Assert.Equal(AppSettings.Defaults, new SettingsStore(_path).Load());
    }

    [Fact]
    public void Flush_AtomicWrite_LeavesNoTempFile() {
        using var store = new SettingsStore(_path);
        store.Save(AppSettings.Defaults);
        store.Flush();

        Assert.True(File.Exists(_path));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void SavedFile_CarriesSchemaVersion() {
        using (var store = new SettingsStore(_path)) {
            store.Save(AppSettings.Defaults);
            store.Flush();
        }

        Assert.Contains("\"SchemaVersion\": 1", File.ReadAllText(_path));
    }

    [Fact]
    public void NullPath_DisablesPersistenceGracefully() {
        using var store = new SettingsStore((string?)null);
        store.Save(AppSettings.Defaults);
        store.Flush();   // no-op, must not throw

        Assert.Equal(AppSettings.Defaults, store.Load());
    }
}
