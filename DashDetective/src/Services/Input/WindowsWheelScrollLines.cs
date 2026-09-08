using DashDetective.Services.Diagnostics;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DashDetective.Services.Input;

/// <summary>
/// Reads <c>SPI_GETWHEELSCROLLLINES</c>, the value the Mouse control panel writes. Read per notch rather
/// than cached: it can change under a running app, and the call costs nothing.
/// </summary>
internal sealed class WindowsWheelScrollLines : IWheelScrollLines {
    private const uint SpiGetWheelScrollLines = 0x0068;

    /// <summary>What the API reports for "one screen at a time".</summary>
    private const uint WheelPageScroll = uint.MaxValue;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(uint action, uint param, ref uint value, uint update);

    /// <summary>Annotated rather than the whole type, so <see cref="Interpret"/> stays covered on the
    /// other legs. The guard lives in <see cref="IWheelScrollLines.ForCurrentPlatform"/>.</summary>
    [SupportedOSPlatform("windows")]
    public WindowsWheelScrollLines() { }

    public int? PerNotch() {
        uint lines = 0;
        try {
            if (!SystemParametersInfoW(SpiGetWheelScrollLines, 0, ref lines, 0))
                return null;
        } catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) {
            Log.Warn("Could not read the wheel scroll setting", e);
            return null;
        }

        return Interpret(lines);
    }

    /// <summary>Maps the raw API value onto the seam's contract, apart from the call so it can be pinned
    /// without a Windows box.</summary>
    internal static int? Interpret(uint lines) =>
        lines == WheelPageScroll ? -1 : (int)lines;
}

/// <summary>The no-data contract where there is no wheel setting to read.</summary>
internal sealed class UnsupportedWheelScrollLines : IWheelScrollLines {
    public int? PerNotch() => null;
}
