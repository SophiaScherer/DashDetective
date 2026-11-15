# File Explorer root crumb on Linux

Work item 138, Sprint 10 exam: *the root crumb is empty on Linux.*

## What was wrong

The breadcrumb trimmed the path separator from the root's name so `C:\` reads `C:`. On Linux the root's
name is `/`, which trimmed to an empty string and left a zero-width, unclickable crumb.

## What was built

- `FileExplorerViewModel.RootLabel` does the trim and falls back to the untrimmed name when nothing is
  left, so the Unix root shows `/`.

## Decisions

- **The helper is pure and `internal`** so the rule runs on both CI legs without a real filesystem root.

## How to verify

1. On Linux, open File Explorer and navigate to any folder.
2. The first crumb reads `/` and selecting it opens the root.
