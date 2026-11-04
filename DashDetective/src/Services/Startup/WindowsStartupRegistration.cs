using DashDetective.Services.Diagnostics;
using Microsoft.Win32;
using System;
using System.Runtime.Versioning;

namespace DashDetective.Services.Startup;

/// <summary>
/// The two registry values that decide whether a Windows app starts at login: the <c>Run</c> entry
/// itself, and the Task Manager switch beside it. A seam so the combining logic runs on every CI leg.
/// </summary>
internal interface IStartupRunStore {
    string? GetRun();
    void SetRun(string command);
    void DeleteRun();

    /// <summary>The raw <c>StartupApproved\Run</c> value, or null when there is none.</summary>
    byte[]? GetApproval();
    void DeleteApproval();
}

/// <summary>
/// Registers (or clears) the app in the per-user Windows startup list — the HKCU <c>Run</c> key, the
/// same mechanism the Task Manager "Startup apps" tab reflects. That tab disables an entry by writing
/// <c>StartupApproved\Run</c> and leaves <c>Run</c> alone, so both are read, and turning the toggle on
/// clears the disabled state. Fully soft-failing, so a locked-down host degrades to "not enabled" rather
/// than crashing. Uses the in-box <c>Microsoft.Win32.Registry</c> API (in-box on the net10.0 target, no
/// package). The platform check lives in <see cref="IStartupRegistration.ForCurrentPlatform"/>; only the
/// parameterless constructor, which builds the registry store, carries the OS attribute.
/// </summary>
internal sealed class WindowsStartupRegistration : IStartupRegistration {
    private readonly IStartupRunStore _store;

    [SupportedOSPlatform("windows")]
    public WindowsStartupRegistration() : this(new RegistryRunStore()) { }

    /// <summary>Test seam: the store, so the logic runs against a fake on any host.</summary>
    internal WindowsStartupRegistration(IStartupRunStore store) => _store = store;

    public bool IsEnabled() {
        try {
            return _store.GetRun() is not null && !StartupApproval.IsDisabled(_store.GetApproval());
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
        if (enabled) {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
                return; // no launchable path (unexpected) — leave the list untouched
            _store.SetRun($"\"{exe}\"");
            // Without this a Task Manager "Disabled" survives the toggle and the app still would not start.
            if (StartupApproval.IsDisabled(_store.GetApproval()))
                _store.DeleteApproval();
        } else if (_store.GetRun() is not null) {
            _store.DeleteRun();
        }
    }

    [SupportedOSPlatform("windows")]
    private sealed class RegistryRunStore : IStartupRunStore {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ValueName = "DashDetective";

        public string? GetRun() {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName)?.ToString();
        }

        public void SetRun(string command) {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key?.SetValue(ValueName, command);
        }

        public void DeleteRun() {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key?.GetValue(ValueName) is not null)
                key.DeleteValue(ValueName);
        }

        public byte[]? GetApproval() {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath);
            return key?.GetValue(ValueName) as byte[];
        }

        public void DeleteApproval() {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

/// <summary>The no-startup-store set: reports "not enabled" and ignores writes — byte-for-byte what
/// the old <c>OperatingSystem.IsWindows()</c> guards returned off Windows.</summary>
internal sealed class UnsupportedStartupRegistration : IStartupRegistration {
    public bool IsEnabled() => false;

    public void SetEnabled(bool enabled) { }
}
