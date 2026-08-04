using DashDetective.Services.Diagnostics;
using Microsoft.Win32;
using System;
using System.Runtime.Versioning;

namespace DashDetective.Services.Startup;

/// <summary>
/// Registers (or clears) the app in the per-user Windows startup list — the HKCU <c>Run</c> key, the
/// same mechanism the Task Manager "Startup apps" tab reflects. Fully soft-failing, so a locked-down
/// host degrades to "not enabled" rather than crashing. Uses the in-box <c>Microsoft.Win32.Registry</c>
/// API (no package on the net10.0-windows target). The platform check lives in
/// <see cref="IStartupRegistration.ForCurrentPlatform"/>, which is why there is no guard in here.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsStartupRegistration : IStartupRegistration {
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DashDetective";

    public bool IsEnabled() {
        try {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is not null;
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

/// <summary>The no-startup-store set: reports "not enabled" and ignores writes — byte-for-byte what
/// the old <c>OperatingSystem.IsWindows()</c> guards returned off Windows.</summary>
internal sealed class UnsupportedStartupRegistration : IStartupRegistration {
    public bool IsEnabled() => false;

    public void SetEnabled(bool enabled) { }
}
