# Export every GPU

Work item 119: *The exported report lists only the first GPU.*

## What was wrong

`HardwareViewModel.GetReportRows` returned a fixed list that named the first Graphics card. A machine
with an integrated and a discrete adapter has one card each, but the export (toolbar Export, Settings
Export report, Copy diagnostics) showed only the first.

## What was built

- `GetReportRows` maps every entry of `Cards` through `ReportRowsOf`, so each Graphics card gets its own
  `Graphics` row, in the order the page shows them.

## Decisions

- **Rows keep the title "Graphics".** The report section is a plain key/value list; the adapters appear in
  page order rather than gaining numbered keys.
- **The mapping is a static helper.** The page cannot be constructed in a plain test (its icons need a
  render backend), so the tests pin the helper over cards built with null icons.

## How to verify

1. On a machine with two GPUs, open Settings and choose Export report.
2. The Hardware section holds one Graphics row per adapter, between Processor and Motherboard.
