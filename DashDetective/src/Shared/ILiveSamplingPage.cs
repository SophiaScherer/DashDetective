namespace DashDetective.Shared;

/// <summary>A page that runs live sampling timers and can be paused/resumed by the toolbar's Live
/// pill. The shell routes the Live toggle to every nav page implementing this (the Dashboard stops
/// its metric timers; the Network tab stops its throughput/diagnostics timers), so a page opts in
/// simply by implementing it — no per-page wiring in the shell. Mirrors <see cref="IRefreshablePage"/>.</summary>
public interface ILiveSamplingPage {
    /// <summary>Pauses (<c>false</c>) or resumes (<c>true</c>) the page's live sampling.</summary>
    void SetLive(bool live);
}
