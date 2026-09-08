using System;

namespace DashDetective.Services.Input;

/// <summary>
/// Reads how far the OS asks a wheel notch to scroll. Implementations must never throw: an unreadable
/// setting degrades to <c>null</c>, which callers read as "use the default".
/// </summary>
internal interface IWheelScrollLines {
    /// <summary>Lines per notch; <c>-1</c> for the OS's "one screen at a time", <c>0</c> for its "do not
    /// scroll", and <c>null</c> where the setting cannot be read.</summary>
    int? PerNotch();

    /// <summary>The setting for this machine — <c>SPI_GETWHEELSCROLLLINES</c> on Windows, "unknown"
    /// elsewhere. Linux keeps it per desktop environment, so there is no single value and no Linux arm.</summary>
    static IWheelScrollLines ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsWheelScrollLines() : new UnsupportedWheelScrollLines();
}
