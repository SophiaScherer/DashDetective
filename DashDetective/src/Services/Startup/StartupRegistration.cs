using DashDetective.Services.Diagnostics;
using Microsoft.Win32;
using System;
using System.Runtime.Versioning;

namespace DashDetective.Services.Startup;

/// <summary>
/// Registers (or clears) the app in the per-user Windows startup list — the HKCU <c>Run</c> key, the
/// same mechanism the Task Manager "Startup apps" tab reflects. A plain static reader/writer like
/// <c>CurrentUserProvider</c>: Windows-guarded and fully soft-failing, so a locked-down or non-Windows
/// host degrades to "not enabled" rather than crashing. Uses the in-box <c>Microsoft.Win32.Registry</c>
/// API (no package on the net10.0-windows target).
/// </summary>
public static class StartupRegistration {
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DashDetective";

    /// <summary>Whether a startup entry for this app currently exists. Safe on any platform.</summary>
    public static bool IsEnabled() {
        if (!OperatingSystem.IsWindows())
            return false;

        try {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is not null;
        } catch (Exception e) {
            Log.Warn("Could not read startup registration", e);
            return false;
        }
    }

    /// <summary>Adds or removes the startup entry. A denied write (or non-Windows host) logs and returns
    /// without throwing, so a failure never propagates into the settings toggle.</summary>
    public static void SetEnabled(bool enabled) {
        if (!OperatingSystem.IsWindows())
            return;

        try {
            Apply(enabled);
        } catch (Exception e) {
            Log.Warn($"Could not {(enabled ? "add" : "remove")} startup registration", e);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Apply(bool enabled) {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null)
            return;

        if (enabled) {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
                return; // no launchable path (unexpected) — leave the list untouched
            key.SetValue(ValueName, $"\"{exe}\"");
        } else if (key.GetValue(ValueName) is not null) {
            key.DeleteValue(ValueName);
        }
    }
}
