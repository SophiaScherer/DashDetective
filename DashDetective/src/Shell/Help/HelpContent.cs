using System.Collections.Generic;

namespace DashDetective.Shell.Help;

/// <summary>
/// The static copy shown in the Help modal: a one-paragraph description of the app plus the
/// orientation tips a first-time user needs. Held as a static table (like <c>HardwareCatalog</c>
/// and <c>FileTypeCatalog</c>) so the content is testable without any UI. The modal's keyboard
/// shortcuts table is not here — it is generated from <c>ShortcutCatalog</c>, which is also what the
/// key handler resolves against, so the two cannot drift apart.
/// </summary>
public static class HelpContent {
    /// <summary>What the app is, in one paragraph, shown above the tips.</summary>
    public const string Description =
        "DashDetective gives you a real-time view of your machine's CPU, memory, GPU, storage and " +
        "network health, alongside tools for browsing files, inspecting running processes and " +
        "reviewing installed hardware — all in one window. Data refreshes live, so you can watch " +
        "load change as you work.";

    /// <summary>Short orientation tips, in display order.</summary>
    public static readonly IReadOnlyList<string> Tips = [
        "Drag the navigation bar by its logo to dock it to any window edge, or collapse it to a slim icon rail.",
        "The Live pill in the toolbar shows whether sampling is running — click it to pause updates while you read a chart.",
        "Performance detects every CPU, GPU and disk on the machine; the toggle above a graph breaks it down into individual cores or engines.",
        "Refresh re-reads the current page, and Export saves a full plain-text system report.",
        "Settings controls the theme, accent colour, refresh interval and navigation position — all of it is remembered between sessions.",
        "Most of the app can be driven from the keyboard — Ctrl+1 to Ctrl+8 jump between tabs, and the full list is below.",
    ];
}
