# Show hidden toggled quickly

Work item 139.

## What was wrong

A folder load was discarded as stale only when its path differed from the pending one. Toggling
Show hidden reloads the same folder, so a slow read started with hidden files included could finish after
the newer read without them, pass the path check, and leave hidden files showing while the box was unchecked.

## What was built

- The stale-load check also compares the load id with the newest request, so the last request wins.
- `FileExplorerViewModel`'s internal constructor takes an optional folder reader, defaulting to
  `DirectoryService.GetEntriesAsync`, so a test can finish reads out of order.

## Decisions

- **The tree is unchanged.** `FileSystemNode.SyncChildrenAsync` has a similar path-only guard, but testing
  it needs a larger seam than the one allowed here, so it is left for its own change.
- **Superseded loads still end quietly.** They call `EndLoad`, which already ignores a load that no longer
  owns the busy flag.

## How to verify

1. Open a folder with hidden files and toggle Show hidden on and off several times in quick succession.
2. With it unchecked, no hidden files are listed.
