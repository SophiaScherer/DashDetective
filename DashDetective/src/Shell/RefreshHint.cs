using DashDetective.Shared;

namespace DashDetective.Shell;

/// <summary>
/// What the toolbar's Refresh button says it will do on the current page.
///
/// Refresh stays enabled everywhere. Disabling it on the pages that ignore it would leave a dead
/// control in the toolbar, and gating it on live sampling would strip Hardware and File Explorer of
/// their only refresh — neither re-enumerates on its own, live or not. The wording carries the
/// difference instead, so the button never lies about what a click will do.
///
/// Pure and free of control types, like <c>GpuNoReadingNote</c>, so it is testable without a render
/// backend or a constructed shell.
/// </summary>
public static class RefreshHint {
    /// <summary>The tooltip for <paramref name="page"/>. <paramref name="live"/> matters only to a page
    /// that samples on its own: while that is running, Refresh is redundant rather than inert.</summary>
    public static string For(object? page, bool live) => page switch {
        not IRefreshablePage => "Nothing to refresh on this page (F5)",
        ILiveSamplingPage when live => "Refresh now — this page is already updating live (F5)",
        _ => "Refresh (F5)",
    };
}
