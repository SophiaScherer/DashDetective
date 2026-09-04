namespace DashDetective.Tabs.Settings;

/// <summary>
/// Every individually addressable setting on the Settings page. Search results and the page's own rows
/// both name a setting by id rather than by its label, so renaming the copy can never break the jump.
/// </summary>
public enum SettingId {
    Theme,
    Accent,
    ClockFormat,
    UiScale,
    HighContrast,
    AccessibilityDefaults,
    NavPosition,
    NavCollapse,
    RefreshInterval,
    ResourceAlerts,
    AlertCpu,
    AlertMemory,
    AlertGpu,
    AlertDiskActivity,
    AlertLowDiskFree,
    AlertSustain,
    NvidiaGpuMetrics,
    ShowInTray,
    LaunchAtStartup,
    Shortcuts,
    WidgetPlacements,
    ExportData,
}
