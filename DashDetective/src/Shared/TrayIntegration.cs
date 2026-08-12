using System;

namespace DashDetective.Shared;

/// <summary>
/// Whether closing the window may hide the app to a tray icon instead of exiting.
///
/// <b>Windows only, deliberately.</b> GNOME — the desktop this port targets — removed its notification
/// area, and Avalonia's tray icon reaches it through StatusNotifierItem, which needs a host that stock
/// GNOME does not run. With the tray setting <i>on by default</i>, honouring it there would hide the
/// window behind an icon that never appears: the app would keep running with no way to get it back, and
/// no way to close it short of killing the process.
///
/// KDE and XFCE do run a host and would work, but there is no reliable way to ask at startup whether an
/// icon will actually be shown — and guessing wrong in that direction strands the user. Exiting on close
/// is the answer that is never wrong.
///
/// The setting itself is kept and still persists, so a preference set on Windows survives a settings
/// file being carried between machines; it is only disabled where it cannot be honoured.
/// </summary>
internal static class TrayIntegration {
    /// <summary>Whether a close should hide to the tray rather than exit, when the setting asks for it.</summary>
    internal static bool HidesOnClose { get; } = OperatingSystem.IsWindows();
}
