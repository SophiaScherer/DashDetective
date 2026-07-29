namespace DashDetective.Tabs.Settings;

/// <summary>One row of the Settings page, described once for both the page and search.</summary>
/// <param name="Id">Which setting this is — what a search result carries to jump back here.</param>
/// <param name="Section">The panel heading it sits under, e.g. "Appearance".</param>
/// <param name="Name">The label shown beside the control.</param>
/// <param name="Description">The line of explanation beneath the label.</param>
/// <param name="Keywords">Extra words a user might search for that the visible copy doesn't contain —
/// "dark" and "light" for the theme picker, "notification" for the alert banner. Never shown.</param>
public sealed record SettingEntry(
    SettingId Id,
    string Section,
    string Name,
    string Description,
    string Keywords = "");
