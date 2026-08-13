using System;

namespace DashDetective.Shared;

/// <summary>
/// Whether closing the window may hide to a tray icon instead of exiting. Windows only: stock GNOME runs
/// no StatusNotifierItem host, and since the setting is on by default, honouring it there would hide the
/// window behind an icon that never appears. KDE and XFCE would work, but nothing can be asked at
/// startup — and guessing wrong strands the user, so exiting on close is the answer that is never wrong.
/// </summary>
internal static class TrayIntegration {
    internal static bool HidesOnClose { get; } = OperatingSystem.IsWindows();
}
