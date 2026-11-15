# Recent searches compare file paths like paths

Work item 123, Sprint 10 exam: *recents compare file paths case-insensitively on every host.*

## What was wrong

`RecentSearches` and `UniversalSearchViewModel.ReopenAsync` compared a recent's key with
`OrdinalIgnoreCase`. For a file the key is a full path, and AGENTS.md says same-path checks follow
`PathComparison`. On Linux, remembering `/a/notes.txt` dropped a different `/a/Notes.txt` recent.
`ReopenAsync` also had a bare `catch` with no comment.

## What was built

- `RecentSearches.KeyComparison(category)`: `PathComparison.Comparison` for files, `OrdinalIgnoreCase` for
  every other category. Both the dedupe and the reopen match use it.
- The bare `catch` in `ReopenAsync` says why it leaves the recent alone.

## Decisions

- **Non-file identities stay case-insensitive** on every host; they are names, not paths.
- **The file-path test can only fail on a case-sensitive host**, so it is the Linux CI leg that guards it.

## How to verify

1. On Linux, open `Notes.txt` and `notes.txt` from search.
2. Both appear in recent searches.
