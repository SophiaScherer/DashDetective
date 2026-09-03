namespace DashDetective.Tabs.Settings;

/// <summary>
/// Which card on the Settings page a setting sits on, as the <c>WidgetId</c> its panel carries.
///
/// Keyed off the catalog's <c>Section</c> rather than the id, so there is no third table to keep in
/// step with the enum: the catalog stays the single source of truth for what a setting belongs to.
/// </summary>
internal static class SettingCards {
    /// <summary>The card for a catalog section, or an empty string for one this page does not draw.</summary>
    internal static string WidgetIdFor(string section) => section switch {
        "Appearance" => "settings.appearance",
        "Navigation" => "settings.navigation",
        "Monitoring" => "settings.monitoring",
        "Alerts" => "settings.alerts",
        "Keyboard" => "settings.keyboard",
        "Layout" => "settings.layout",
        "Export & Data" => "settings.export",
        _ => "",
    };
}
