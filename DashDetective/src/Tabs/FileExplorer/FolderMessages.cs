namespace DashDetective.Tabs.FileExplorer;

/// <summary>What an empty file list should say: a title plus a line telling the user what to do
/// about it.</summary>
internal readonly record struct FolderMessage(string Title, string Hint);

/// <summary>
/// Why the file list is blank, worded. Six situations used to render as the same empty pane (three of
/// them as the literal words "This folder is empty"), so the user could not tell a protected folder
/// from an empty one, or a filter that hid everything from a folder with nothing in it.
///
/// Pure and render-free on purpose: <see cref="FileExplorerViewModel"/> cannot be tested at all — it
/// reaches <see cref="FileTypeCatalog"/>, whose initializer needs a render backend — so the decision
/// lives here instead, the way <see cref="FileExplorerPanes"/> and <see cref="FileExplorerTableLayout"/>
/// hold their rules.
/// </summary>
internal static class FolderMessages {
    /// <summary>The message for the current state, or null when the list itself should show. Order
    /// matters: the first matching situation wins.</summary>
    internal static FolderMessage? Resolve(
        bool isReading, bool hasFolder, FolderReadStatus status,
        int totalCount, int visibleCount, string filterLabel) {
        // A folder that has not answered yet has nothing to say about why it is empty — without this
        // every navigation flashes "This folder is empty" until the read lands.
        if (isReading)
            return null;

        if (!hasFolder)
            return new FolderMessage(
                "No folder open",
                "Pick a drive or folder in the tree to start browsing.");

        switch (status) {
            case FolderReadStatus.AccessDenied:
                return new FolderMessage(
                    "You don't have permission to view this folder",
                    "Its contents are protected by the system.");
            case FolderReadStatus.NotFound:
                return new FolderMessage(
                    "This folder no longer exists",
                    "It may have been moved, renamed, or deleted.");
            case FolderReadStatus.Unreadable:
                return new FolderMessage(
                    "This folder couldn't be read",
                    "Something went wrong reading it. Try Refresh.");
            case FolderReadStatus.HiddenOnly:
                return new FolderMessage(
                    "Everything here is hidden",
                    "Turn on Show hidden under Options to see it.");
        }

        if (totalCount == 0)
            return new FolderMessage(
                "This folder is empty",
                "There's nothing here to show.");

        // The folder has entries; the active chip is what is hiding them.
        if (visibleCount == 0)
            return new FolderMessage(
                $"No {filterLabel} in this folder",
                "Choose All to see everything here.");

        return null;
    }
}
