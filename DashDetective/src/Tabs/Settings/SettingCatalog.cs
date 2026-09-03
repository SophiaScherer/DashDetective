using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// The single source of truth for what each setting is called and what it does — the same role
/// <c>ShortcutCatalog</c> plays for the keyboard.
///
/// The page's labels bind to these entries rather than holding literals of their own, so the copy on
/// screen is by construction the copy search matches against: a reworded description is found under its
/// new wording with no second edit. Held as a singleton (rather than a static table) purely so the XAML
/// can reach it through a plain compiled binding — <c>{Binding Catalog.Theme.Name}</c>.
/// </summary>
public sealed class SettingCatalog {
    public static SettingCatalog Instance { get; } = new();

    private SettingCatalog() {
        All = [
            Theme, Accent, ClockFormat,
            NavPosition, NavCollapse,
            RefreshInterval, ResourceAlerts, NvidiaGpuMetrics, ShowInTray, LaunchAtStartup,
            AlertCpu, AlertMemory, AlertGpu, AlertDiskActivity, AlertLowDiskFree, AlertSustain,
            Shortcuts,
            WidgetPlacements,
            ExportData,
        ];
    }

    // ----- Appearance -----

    // Matching is over contiguous text, so the phrases people actually type ("dark mode") are written
    // out as phrases rather than left as loose words that only match one at a time.
    public SettingEntry Theme { get; } = new(
        SettingId.Theme, "Appearance", "Theme", "Choose the application color scheme",
        Keywords: "dark mode light mode system colour color appearance");

    public SettingEntry Accent { get; } = new(
        SettingId.Accent, "Appearance", "Accent color", "Applied to charts and highlights",
        Keywords: "accent colour highlight chart swatch");

    public SettingEntry ClockFormat { get; } = new(
        SettingId.ClockFormat, "Appearance", "Clock format", "Show times as 24-hour or 12-hour",
        Keywords: "12 hour 24 hour am pm military clock time");

    // ----- Navigation -----

    public SettingEntry NavPosition { get; } = new(
        SettingId.NavPosition, "Navigation", "Position", "Which edge the navigation bar docks to",
        Keywords: "sidebar dock left right top bottom move nav bar");

    public SettingEntry NavCollapse { get; } = new(
        SettingId.NavCollapse, "Navigation", "Collapse to icons",
        "Show only the icons in the navigation bar",
        Keywords: "sidebar collapse expand icons only narrow nav bar");

    // ----- Monitoring -----

    public SettingEntry RefreshInterval { get; } = new(
        SettingId.RefreshInterval, "Monitoring", "Refresh interval", "How often live metrics update",
        Keywords: "poll rate sampling speed seconds update frequency");

    public SettingEntry ResourceAlerts { get; } = new(
        SettingId.ResourceAlerts, "Monitoring", "Resource alerts",
        "Show a banner when a resource crosses its threshold",
        Keywords: "notification warning banner threshold high usage");

    // Off by default because it is the one metric in the app that costs a process launch to read. The
    // copy says what it costs rather than naming the tool, which means nothing to most people — and
    // where nothing has to be launched it says that instead, like ShowInTray. See SettingDescriptions.
    public SettingEntry NvidiaGpuMetrics { get; } = new(
        SettingId.NvidiaGpuMetrics, "Monitoring", "NVIDIA GPU utilization",
        SettingDescriptions.NvidiaGpuMetrics,
        Keywords: "nvidia gpu graphics utilisation utilization nvidia-smi linux");

    // Kept and still searchable where the tray cannot be honoured — only its copy and its toggle change,
    // so the setting does not vanish from search on one platform. See SettingDescriptions.
    public SettingEntry ShowInTray { get; } = new(
        SettingId.ShowInTray, "Monitoring", "Show in system tray", SettingDescriptions.ShowInTray,
        Keywords: "tray notification area minimise minimize close background");

    // The one description that names its mechanism rather than its effect, so it is the one that has to
    // vary by platform — see SettingDescriptions.
    public SettingEntry LaunchAtStartup { get; } = new(
        SettingId.LaunchAtStartup, "Monitoring", "Launch at startup", SettingDescriptions.LaunchAtStartup,
        Keywords: "boot autostart auto start login run on startup");

    // ----- Alerts -----

    // One entry per watched resource rather than one for the card: a threshold is the thing people come
    // looking for ("cpu alert 90"), and each has to be revealable on its own.
    public SettingEntry AlertCpu { get; } = new(
        SettingId.AlertCpu, "Alerts", "CPU usage", "Warn when processor use stays this high",
        Keywords: "cpu processor alert threshold percent high usage warning");

    public SettingEntry AlertMemory { get; } = new(
        SettingId.AlertMemory, "Alerts", "Memory usage", "Warn when RAM use stays this high",
        Keywords: "memory ram alert threshold percent high usage warning");

    // Off by default, and the copy says why: sustained saturation is what legitimate heavy work looks
    // like on both of these, so watching them by default would mostly report the machine doing its job.
    public SettingEntry AlertGpu { get; } = new(
        SettingId.AlertGpu, "Alerts", "GPU usage",
        "Warn when any graphics adapter stays this busy. Off by default — games and renders live here",
        Keywords: "gpu graphics adapter alert threshold percent high usage warning");

    public SettingEntry AlertDiskActivity { get; } = new(
        SettingId.AlertDiskActivity, "Alerts", "Disk activity",
        "Warn when any drive stays this busy. Off by default — large copies and updates live here",
        Keywords: "disk drive activity busy alert threshold percent warning io");

    public SettingEntry AlertLowDiskFree { get; } = new(
        SettingId.AlertLowDiskFree, "Alerts", "Low disk space",
        "Warn when any drive drops below this much free space",
        Keywords: "disk space free full storage low alert threshold warning capacity");

    public SettingEntry AlertSustain { get; } = new(
        SettingId.AlertSustain, "Alerts", "Warn after",
        "How long usage must stay over a threshold before it counts",
        Keywords: "sustain duration delay how long seconds alert threshold debounce");

    // ----- Keyboard -----

    // One entry for the whole card, like ExportData below: search should land someone on the list, and
    // an entry per binding would bury every other setting under thirty near-identical rows.
    public SettingEntry Shortcuts { get; } = new(
        SettingId.Shortcuts, "Keyboard", "Keyboard shortcuts", "Change the keys any action is bound to",
        Keywords: "keyboard shortcut hotkey key binding keybinding rebind remap customize reset");

    // ----- Layout -----

    // One entry for the card, like Shortcuts and ExportData: the reset is the whole card.
    public SettingEntry WidgetPlacements { get; } = new(
        SettingId.WidgetPlacements, "Layout", "Widget placements",
        "Put every page's widgets and cards back in the order they ship in",
        Keywords: "widget card panel tile layout order arrange rearrange reorder drag drop reset restore default position placement");

    // ----- Export & Data -----

    public SettingEntry ExportData { get; } = new(
        SettingId.ExportData, "Export & Data", "Export & Data",
        "Export a report or CSV, or copy diagnostics to the clipboard",
        Keywords: "export report csv save diagnostics clipboard copy download");

    /// <summary>Every entry, in the order the page lists them.</summary>
    public IReadOnlyList<SettingEntry> All { get; }

    /// <summary>The entry for an id. Throws for an id with no entry, which would mean the enum and this
    /// table have drifted apart — a bug, not a runtime condition (and one the tests catch).</summary>
    public SettingEntry Get(SettingId id) {
        foreach (var entry in All)
            if (entry.Id == id)
                return entry;

        throw new ArgumentOutOfRangeException(nameof(id), id, "No catalog entry for this setting.");
    }
}
