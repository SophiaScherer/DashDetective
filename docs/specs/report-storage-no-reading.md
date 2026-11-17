# Report Storage row without a system disk

Work item 135, Sprint 10 exam: *the report prints "Storage: 0% active ()" when the system disk is not found.*

## What was wrong

The Dashboard's text report built its Storage row from the system disk's activity and capacity. When no
volume identified the system disk, `_systemDiskNumber` stayed `-1`, nothing ever updated the values, and
the row read `0% active  ()` for the whole session, a confident wrong answer.

## What was built

- The Storage row is `Placeholders.NoReading` ("—") while no system disk is known.

## Decisions

- **Only the report changes.** The values are not displayed on a card, and the CSV export keeps its own
  column handling.

## How to verify

1. On a machine where the system volume cannot be matched to a disk, copy the diagnostics report.
2. The Storage line reads "—", not "0% active  ()".
