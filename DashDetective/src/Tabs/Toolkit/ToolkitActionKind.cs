namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// How an entry is carried out. Distinct from <see cref="ToolkitEntryKind"/>, which is presentation
/// only: two entries can wear the same badge and run down different paths (a <c>Panel</c> is launched,
/// a <c>Command</c> may be captured or elevated).
///
/// Only <see cref="Capture"/> redirects output. <see cref="Elevated"/> cannot: Windows refuses to
/// redirect the streams of a process started through the <c>runas</c> verb, which is why elevation is
/// its own kind rather than a flag on <see cref="Capture"/>.
/// </summary>
public enum ToolkitActionKind {
    /// <summary>Hand a filesystem path (or a <c>shell:</c> location) to the shell — a folder in Explorer.</summary>
    OpenPath,

    /// <summary>Hand an <c>https://</c> URL to the shell, opening the default browser.</summary>
    OpenUrl,

    /// <summary>Start an executable and leave it running in its own window; nothing is captured.</summary>
    Launch,

    /// <summary>Start an executable with its output redirected into the Execution Log, under a timeout.</summary>
    Capture,

    /// <summary>Start an executable through the <c>runas</c> verb, raising the UAC prompt. No capture.</summary>
    Elevated,
}
