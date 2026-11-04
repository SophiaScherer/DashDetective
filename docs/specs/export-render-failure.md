# Keep a failed export from truncating the file

Work item 109.

## What was wrong

`FileSave.SaveAsync` opened the chosen destination for writing and only then rendered the report. The
open truncates the file, so a renderer that threw left an empty or partial file behind. A single
`catch (Exception)` then returned null, which callers read as a cancelled pick, so nothing told the user.
The catch was also wider than its comment (disk full, permission denied) and logged nothing.

## What was built

- The report is rendered to a string before the destination is opened (`FileSave.TryWriteAsync`).
- A renderer failure is caught (broadly, since what a renderer throws is not knowable), logged through
  `Log.Error` and returns null without the destination having been opened.
- The write catch admits only `IOException` and `UnauthorizedAccessException`, and logs through `Log.Warn`.

## Decisions

- **A renderer failure does not propagate.** Every caller is an `async void` handler (or a fire-and-forget
  shortcut), so a throw would crash the app. It is logged as an error and returns null; the existing file
  is untouched because nothing was opened. Callers still cannot tell it from a cancel, which is accepted.
- **A failed I/O write still returns null**, as SOURCE-MAP documents, so all four callers keep one value
  to confirm from.

## How to verify

1. Run the `FileSaveTests` fixture: a throwing renderer never opens the destination, and an I/O failure
   returns false.
2. Export a report over an existing file: the file is replaced with the complete report.
