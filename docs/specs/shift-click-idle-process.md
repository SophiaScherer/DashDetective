# Shift-click after the System Idle Process

Work item 140, Sprint 10 exam: *a Shift-click range fails after clicking the System Idle Process.*

## What was wrong

The Processes tab remembered where a Shift-click measures from as an `int`, with `0` meaning "no anchor".
PID 0 is the System Idle Process, a real row. After clicking it the anchor read as unset, so the next
Shift-click replaced the selection with one row instead of taking the range.

## What was built

- The anchor is `int?`; null means none, and PID 0 anchors like any other row.

## Decisions

- **The anchor is still cleared** when its row leaves the selection or the selection is cleared, now by
  setting it to null.

## How to verify

1. Click System Idle Process, then Shift-click a row below it.
2. Every row between them is selected.
