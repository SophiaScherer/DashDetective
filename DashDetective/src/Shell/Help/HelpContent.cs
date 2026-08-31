using System.Collections.Generic;

namespace DashDetective.Shell.Help;

/// <summary>
/// The static copy shown in the Help modal: a one-paragraph description of the app, a tour of the
/// pages, and the orientation tips a first-time user needs. Held as a static table (like
/// <c>HardwareCatalog</c> and <c>FileTypeCatalog</c>) so the content is testable without any UI. The
/// modal's keyboard shortcuts table is not here — it is generated from <c>ShortcutCatalog</c>, which is
/// also what the key handler resolves against, so those two cannot drift apart.
/// </summary>
public static class HelpContent {
    /// <summary>What the app is, in one paragraph, shown above the tour.</summary>
    public const string Description =
        "DashDetective gives you a real-time view of your machine's CPU, memory, GPU, storage and " +
        "network health, alongside tools for browsing files, inspecting running processes and " +
        "reviewing installed hardware — all in one window. Data refreshes live, so you can watch " +
        "load change as you work.";

    /// <summary>A line per page, then the shell surfaces that are not pages. The pages are listed in
    /// navigation order, which is also the order Ctrl and a number jump to them. Restated here rather
    /// than read from the nav table: a <c>NavItem</c> carries a <c>Geometry</c> icon, which cannot be
    /// built without a render backend, and this copy has to stay testable headlessly.</summary>
    public static readonly IReadOnlyList<HelpTopic> GettingStarted = [
        new("page.dashboard", "Dashboard",
            "A real-time overview of the whole machine: CPU, memory, GPU, disk and network at a glance."),
        new("page.fileExplorer", "File Explorer",
            "Browse files and folders, with sizes and types, and an address bar you can type a path into."),
        new("page.processes", "Processes",
            "Every running process with its CPU, memory and disk use. Sort on any column, or end a process you recognize."),
        new("page.performance", "Performance",
            "Live graphs per resource. Every CPU, GPU and disk on the machine is detected, and a graph can be broken down into individual cores or engines."),
        new("page.network", "Network",
            "Your adapters and their throughput, plus the connections the machine currently holds open."),
        new("page.storage", "Storage",
            "Drives and partitions with free space, activity and health — including drive temperature where the disk reports it."),
        new("page.hardware", "Hardware",
            "A spec sheet of what is installed: processor, motherboard, memory, graphics and disks."),
        new("page.toolkit", "Toolkit",
            "Common diagnostic commands, ready to run. Pin the ones you use often, or add your own."),
        new("page.settings", "Settings",
            "Everything you can change: appearance, navigation, monitoring, resource alerts, keyboard shortcuts and export."),
        new("shell.search", "Search",
            "Ctrl+F searches pages, settings, keyboard shortcuts, Toolkit commands, running processes and your files at once."),
        new("shell.toolbar", "The toolbar",
            "The Live pill shows whether sampling is running, Refresh re-reads the current page, Export saves a system report, and the clock follows the format you chose."),
        new("shell.navigation", "The navigation bar",
            "Drag it by the logo to dock it to any window edge, or collapse it to a slim icon rail."),
    ];

    /// <summary>Short orientation tips, in display order.</summary>
    public static readonly IReadOnlyList<HelpTopic> Tips = [
        new("tip.search", null,
            "Ctrl+F searches everything at once — pages, settings, keyboard shortcuts, Toolkit commands, running processes and your files. Pick a result and it takes you there and highlights it."),
        new("tip.slash", null,
            "Press / to jump straight to the current page's own field: the process filter, the Toolkit command filter, or the File Explorer address bar."),
        new("tip.completion", null,
            "Where a field can guess what you are typing, the rest of the suggestion appears grayed out after the caret — press Tab to accept it."),
        new("tip.navigation", null,
            "Drag the navigation bar by its logo to dock it to any window edge, or press Ctrl+B to collapse it to a slim icon rail."),
        new("tip.live", null,
            "The Live pill in the toolbar shows whether sampling is running — click it or press Ctrl+P to pause updates while you read a chart."),
        new("tip.performance", null,
            "Performance detects every CPU, GPU and disk on the machine; the toggle above a graph breaks it down into individual cores or engines."),
        new("tip.export", null,
            "Export saves a full system report as text, JSON, Markdown, HTML or CSV — the format follows the extension you type in the save dialog."),
        new("tip.diagnostics", null,
            "Settings also offers Copy diagnostics, which puts the same report on the clipboard, and Export CSV, which saves the recent metric history."),
        new("tip.alerts", null,
            "Turn on resource alerts in Settings to get a banner when CPU, memory, GPU, disk activity or free disk space crosses a threshold you set."),
        new("tip.shortcuts", null,
            "Any keyboard shortcut can be rebound in Settings under Keyboard, and Restore default shortcuts puts them all back."),
        new("tip.theme", null,
            "Ctrl+Shift+T flips between the light and dark theme; Settings also has a system option and a choice of accent colors."),
        new("tip.persistence", null,
            "Your choices are remembered between sessions — theme, accent, clock format, refresh interval, navigation position, alert thresholds and any shortcuts you rebound."),
        new("tip.keyboard", null,
            "Most of the app can be driven from the keyboard — Ctrl and a number jump to a page by position, and the full list is under Shortcuts."),
    ];
}
