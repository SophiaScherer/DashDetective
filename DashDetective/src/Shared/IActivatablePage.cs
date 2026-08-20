namespace DashDetective.Shared;

/// <summary>A page whose work should only run while it is on screen. The shell routes activation to
/// every nav page implementing this — the current page when the window is visible, nothing otherwise —
/// so a page opts in simply by implementing it, exactly as <see cref="IRefreshablePage"/> and
/// <see cref="ILiveSamplingPage"/> do. Independent of the Live pill: a page runs only when the user
/// wants live data <em>and</em> is looking at it.</summary>
public interface IActivatablePage {
    /// <summary>Activates (<c>true</c>) or deactivates (<c>false</c>) the page.</summary>
    void SetActive(bool active);
}
