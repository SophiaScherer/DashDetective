# A manual selection cancels a pending reveal

Work item 136, Sprint 10: *A pending Performance reveal overrides a later manual selection.*

## What was wrong

A Dashboard card jump names a device through `PerformanceViewModel.Reveal`. An id whose row is not built
yet (a disk or GPU before the inventory loads) is held in `_pendingReveal` and applied when the rows
arrive. Clicking a row in the meantime did not clear it, so the late inventory selected the revealed row
and pulled the page away from the one the user had just chosen.

## What was built

- Every row's `SelectCommand` now routes through `SelectManually`, which clears `_pendingReveal` and then
  selects. The reveal's own selection and the rebuild's survivor selection still use `Select` directly.

## Decisions

- **Only a click cancels the reveal.** A rebuild that re-selects the previous row is not a user choice and
  must not drop a reveal still waiting for its row.

## How to verify

1. Open the app and jump from a Dashboard disk card before the Performance inventory has loaded.
2. Click another row on Performance before the disk row appears: the page stays on the clicked row.
