namespace DashDetective.Services.Notifications;

/// <summary>
/// The confirmation copy, in one file. Same reason <c>SettingCatalog</c> exists: the wording is the
/// product, not an implementation detail of whichever handler happens to raise it, and four call sites
/// announce a saved export.
/// </summary>
internal static class Notices {
    /// <summary>A file was written. Names the path, because a save dialog has already closed over it by
    /// the time the banner appears.</summary>
    public static string Exported(string path) => $"Exported to {path}";

    /// <summary>Every page's widgets and cards are back in their declared order.</summary>
    public const string WidgetPlacementsReset = "Widget positions reset";

    /// <summary>Every shortcut is back on its shipped gesture.</summary>
    public const string ShortcutsRestored = "Keyboard shortcuts restored to defaults";

    /// <summary>One shortcut is. Names the action rather than the key, which the row already shows.</summary>
    public static string ShortcutRestored(string action) => $"Shortcut restored for {action}";

    /// <summary>A web address could not be handed to a browser. Names it, since nothing appeared.</summary>
    public static string CouldNotOpenLink(string url) => $"Could not open {url}";

    /// <summary>The diagnostics report is on the clipboard.</summary>
    public const string DiagnosticsCopied = "Diagnostics copied to clipboard";
}
