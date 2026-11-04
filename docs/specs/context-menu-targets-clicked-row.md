# The row menu acts on the right-clicked row

Work item 115: *The Processes context menu acts on the last-clicked row, not the right-clicked one.*

## What was wrong

A right-click inside an existing multi-selection left the selection alone, which is intended, but also
left the primary row (`SelectedRow`) on whichever row was clicked last. The menu's Properties, Copy PID
and Expand items act on the primary row, so they hit a different process from the one under the pointer.

## What was built

- `ProcessesViewModel.PrepareContextMenu(row)`: a row outside the selection replaces it, as before; a row
  inside it becomes the primary row and the selection is kept.
- The view's right-press handler calls it instead of selecting inline.

## Decisions

- **The selection is kept.** End task still acts on every selected row, which is why the multi-selection
  survives the right-click.
- **The decision is in the view model** so it can be tested without a pointer.

## How to verify

1. Select two rows with Ctrl-click, ending on row B.
2. Right-click row A and choose Copy PID. A's PID is copied, and both rows stay selected.
3. Right-click an unselected row. The selection becomes that row alone.
