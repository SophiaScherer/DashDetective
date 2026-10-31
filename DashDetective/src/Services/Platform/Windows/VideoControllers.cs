using DashDetective.Services.Diagnostics;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Security;

namespace DashDetective.Services.Platform.Windows;

/// <summary>One physical display adapter as <c>Win32_VideoController</c> reports it. Fields are "" when
/// WMI leaves them blank.</summary>
internal sealed record VideoController(string PnpDeviceId, string Name, string DriverVersion);

/// <summary>
/// The display-adapter reads two tabs share: the Hardware tab's Graphics cards and the GPU details on the
/// Performance tab, so both show the same driver version for the same adapter. <see cref="Read"/> is
/// transparent like <see cref="WmiRead.ForEach(string, Action{System.Management.ManagementBaseObject})"/>:
/// it does not catch, and each caller keeps its own soft-fail.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class VideoControllers {
    /// <summary>Every PCI-bus adapter. Virtual and software adapters (<c>ROOT\</c>, <c>SWD\</c>) are
    /// skipped: physical GPUs sit on the PCI bus.</summary>
    public static IReadOnlyList<VideoController> Read() {
        var controllers = new List<VideoController>();
        WmiRead.ForEach("SELECT Name, PNPDeviceID, DriverVersion FROM Win32_VideoController", obj => {
            var pnp = WmiRead.Text(obj, "PNPDeviceID");
            if (!pnp.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase))
                return;
            controllers.Add(new VideoController(pnp, WmiRead.Text(obj, "Name"), WmiRead.Text(obj, "DriverVersion")));
        });
        return controllers;
    }

    /// <summary>
    /// The device's raw <c>LocationInformation</c> value from its <c>Enum</c> key — the string Device
    /// Manager's Location row and Task Manager's "Physical location" are built from. Readable without admin.
    /// Null when the key or value is absent or the read is refused.
    /// </summary>
    public static string? LocationInformation(string pnpDeviceId) {
        try {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + pnpDeviceId);
            return key?.GetValue("LocationInformation") as string;
        } catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException) {
            Log.Warn($"Location of {pnpDeviceId} unreadable", e);
            return null;
        }
    }
}
