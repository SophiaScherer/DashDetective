# Read a folder once when the tree follows a navigation

Work item 106: *File Explorer reads a folder twice on non-tree navigation, and a search jump loses the
file selection.*

## What was wrong

Back, Up, a breadcrumb, a folder opened from the list, and a universal-search jump all open the folder
through `SetCurrentFolder`. That method starts the read, then `SyncTreeSelection` highlights the
folder's tree node if its branch is loaded. Setting that node's `IsSelected` raised `OnNodeSelected`,
which opened the same folder a second time. The second read had no reselect path, so the folder was
read twice, and a search jump's selected file survived only when the first read happened to finish last.

## What was built

- `OnNodeSelected` skips the navigation when the node is the folder already open. The highlight still
  moves, and a tree click on any other folder navigates as before.
- An `internal PendingLoads` task tracks the root and folder loads in flight, so tests can await the
  page instead of sleeping.

## Decisions

- **The guard is in `OnNodeSelected`, not in `SyncTreeSelection`.** Selecting the node of the folder
  already open is never a navigation, whatever set it, so the rule belongs to the one method that
  navigates from the tree.
- **The test counts reads, not the selection.** A `FileEntry` reaches `FileTypeCatalog`'s geometry, and
  the tests have no render backend, so the shell fake's type-name calls stand in for the read count.
  One read leaves no later load to clear the selection.

## How to verify

1. Open File Explorer and expand a drive so a few subfolders are loaded in the tree.
2. Double-click one of those subfolders in the file list. Its node highlights in the tree, and the list
   loads once with no second flicker.
3. Use universal search to jump to a file inside a folder whose tree node is loaded. The folder opens
   with that file selected and the details pane filled.
4. Press Back and Up. The tree highlight follows each move.
5. Click a different folder in the tree. It opens as before.
