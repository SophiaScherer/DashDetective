# Reveal unfolds the target's group

Work item 116: *Jumping to a process in a folded Processes group selects a row nobody can see.*

## What was wrong

`ProcessesViewModel.Reveal` filtered to the process, expanded its ancestors and selected its row, but
never touched `_collapsedGroups`. When the process sat in a folded Apps, Background or Windows section,
the row was selected inside a hidden list, so the jump looked like it did nothing.

## What was built

- `Reveal` unfolds the target's group (the category of its root node) through `ToggleGroup` before it
  filters and selects.

## Decisions

- **Only the target's group opens.** Other folded groups stay folded.
- **It goes through `ToggleGroup`**, so a remembered fold state is saved the same way a click on the
  header would save it.

## How to verify

1. Fold the Apps group, then search for an app from the toolbar and open its Processes hit.
2. The Apps group opens and the process row is selected.
3. A folded Background group, if any, stays folded.
