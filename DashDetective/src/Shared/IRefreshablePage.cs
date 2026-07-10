namespace DashDetective.Shared;

/// <summary>A page that can re-read its data on demand, driven by the toolbar's Refresh action.
/// The shell routes Refresh to whichever <see cref="ISelfScrollingPage"/>/page is current, so each
/// page decides what "refresh" means (the Dashboard re-samples metrics; the File Explorer reloads
/// the current folder). Pages that opt out simply don't implement this.</summary>
public interface IRefreshablePage {
    void Refresh();
}
