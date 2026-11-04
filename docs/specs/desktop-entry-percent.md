# Escape percent in the autostart Exec line

Work item 133.

## What was wrong

`DesktopEntry.QuoteExec` escaped the four characters the spec reserves inside a quoted argument but not
`%`. The launcher reads `%` in `Exec` as a field-code prefix, so an install path containing one (for
example `/home/a%b/App`) broke autostart.

## What was built

- `%` is written as `%%`.
- A literal backslash is written as four backslashes. The spec applies the string-level escape (`\` to
  `\`) before the quoting rule, so two backslashes in the file reach the quoting rule as one and escape
  the next character instead.

## Decisions

- **Only the backslash gets the second level.** `"`, `` ` `` and `$` keep a single backslash, which
  GLib-based launchers accept; the backslash is the case the spec states outright.

## How to verify

1. Install DashDetective under a folder with a `%` in its name and enable Launch at startup on Linux.
2. The `.desktop` file's `Exec` reads `%%` for each `%`, and the app starts at login.
