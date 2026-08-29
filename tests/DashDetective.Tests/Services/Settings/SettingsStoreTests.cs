using DashDetective.Services.Settings;
using DashDetective.Services.Theming;
using DashDetective.Shared;
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
            ClockFormat = ClockFormat.TwelveHour,
            AlertCpuPercent = 80,
            AlertMemoryPercent = 95,
            AlertGpuPercent = 90,
            AlertDiskActivePercent = 70,
            AlertLowDiskFreePercent = 5,
            AlertSustainSeconds = 30,
            RefreshIntervalSeconds = 2,
            LaunchAtStartup = true,
            PerformanceShowAllDevices = true,
            CpuDetailedView = true,
            GpuDetailedView = true,
            NvidiaGpuMetrics = true,
            TrayNoticeShown = true,
            // Carries the ASCII record separator the pin encoder uses, so the round trip proves JSON
            // escapes and restores it rather than eating a control character.
            PinnedCommands = "%temp%\u001Eipconfig /all",
            ProcessColumns = "Name\u001FCpu\u001FPid",
            ProcessesRememberCollapsed = true,
            ProcessesRememberSort = true,
            ProcessesCollapsedSections = "Background\u001FWindows",
            ProcessesSort = "Cpu\u001FDesc",
        };

        using (var store = new SettingsStore(_path)) {
            store.Save(settings);
            store.Flush();
        }

        var loaded = new SettingsStore(_path).Load();
        Assert.Equal(settings, loaded);
    }

    /// <summary>A settings file written before the alert thresholds existed must come back carrying their
    /// defaults, not zeros — zero is how a metric is switched OFF, so a silent zero would disable the CPU
    /// and memory alerts of every existing install.</summary>
    [Fact]
    public void Load_FileFromBeforeTheAlertThresholds_KeepsTheirDefaults() {
        File.WriteAllText(_path, """
            { "SchemaVersion": 1, "Theme": "Dark", "ResourceAlerts": true }
            """);

        var loaded = new SettingsStore(_path).Load();

        Assert.True(loaded.ShowInTray);   // the pre-existing non-default initializer, same rule
        Assert.Equal(90, loaded.AlertCpuPercent);
        Assert.Equal(90, loaded.AlertMemoryPercent);
        Assert.Equal(10, loaded.AlertLowDiskFreePercent);
        Assert.Equal(10, loaded.AlertSustainSeconds);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults() {
        Assert.Equal(AppSettings.Defaults, new SettingsStore(_path).Load());
    }

    /// <summary>A fresh install has not been told the app keeps running in the tray, so the notice is
    /// still owed. Pinned separately because the flag is a disclosure record, not a preference: a default
    /// of true would silently skip the one time it is ever shown.</summary>
    [Fact]
    public void Defaults_StillOweTheTrayNotice() {
        Assert.False(AppSettings.Defaults.TrayNoticeShown);
        Assert.True(AppSettings.Defaults.ShowInTray);
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
