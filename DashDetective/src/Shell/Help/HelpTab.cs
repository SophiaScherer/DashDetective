namespace DashDetective.Shell.Help;

/// <summary>Which section of Help the modal is showing. <see cref="Overview"/> is not a section of its
/// own — it shows every other one, and is where the modal always opens.</summary>
public enum HelpTab {
    Overview,
    GettingStarted,
    Tips,
    Shortcuts,
}
