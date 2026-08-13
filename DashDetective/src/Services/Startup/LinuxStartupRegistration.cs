using DashDetective.Services.Diagnostics;
using System;
using System.IO;

namespace DashDetective.Services.Startup;

/// <summary>
/// Registers (or clears) the app in the XDG autostart directory, the per-user counterpart of the Windows
/// <c>Run</c> key and just as soft-failing: a read-only home degrades to "not enabled" rather than
/// crashing. Portable managed <c>System.IO</c>, so no <c>[SupportedOSPlatform]</c>.
/// </summary>
internal sealed class LinuxStartupRegistration : IStartupRegistration {
    private const string FileName = "DashDetective.desktop";

    private readonly string _autostartDirectory;

    public LinuxStartupRegistration() : this(DefaultDirectory()) { }

    /// <summary>Test seam: injects the directory so the write path runs against a temp folder from any
    /// dev machine.</summary>
    internal LinuxStartupRegistration(string autostartDirectory) =>
        _autostartDirectory = autostartDirectory;

    // A real filesystem path, so Path.Combine is right here — the never-Path.Combine rule is about
    // /proc and /sys literals, which must stay forward-slashed to match the fixtures.
    private string EntryPath => Path.Combine(_autostartDirectory, FileName);

    public bool IsEnabled() {
        try {
            return File.Exists(EntryPath) && DesktopEntry.IsEnabled(File.ReadAllText(EntryPath));
        } catch (Exception e) {
            Log.Warn("Could not read startup registration", e);
            return false;
        }
    }

    /// <summary>A denied write logs and returns without throwing, so a failure never propagates into
    /// the settings toggle.</summary>
    public void SetEnabled(bool enabled) {
        try {
            Apply(enabled);
        } catch (Exception e) {
            Log.Warn($"Could not {(enabled ? "add" : "remove")} startup registration", e);
        }
    }

    private void Apply(bool enabled) {
        if (!enabled) {
            File.Delete(EntryPath); // no-op when it was never written
            return;
        }

        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            return; // no launchable path (unexpected) — leave the directory untouched

        Directory.CreateDirectory(_autostartDirectory);
        File.WriteAllText(EntryPath, DesktopEntry.Build(exe));
    }

    /// <summary>A relative <c>XDG_CONFIG_HOME</c> is ignored rather than resolved against the working
    /// directory: the spec says it must be absolute or be treated as unset.</summary>
    private static string DefaultDirectory() {
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(config) || !Path.IsPathRooted(config))
            config = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        return Path.Combine(config, "autostart");
    }
}
