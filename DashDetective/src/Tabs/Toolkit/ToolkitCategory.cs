namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The section a command is filed under. Declaration order is display order — the list renders one
/// section per value, in this order, and the filter chips follow it.
/// </summary>
public enum ToolkitCategory {
    /// <summary>Commands the user authored themselves. First, so your own rows are the ones you land
    /// on; a custom command may also carry one of the categories below, and then shows in both.</summary>
    Custom,

    Folders,
    SystemTools,
    Diagnostics,
    DocsAndLinks,
}
