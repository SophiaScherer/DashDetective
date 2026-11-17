# Completion suffix follows the theme

Work item 124.

## What was wrong

The ghosted completion suffix copied the ghost block's brush once, when it was built. Switching theme
re-resolved the block's brush but not the copy, so the suffix kept the previous theme's color until the
next keystroke rebuilt it.

## What was built

- `GhostCompletionBox` keeps a reference to the suffix run and re-applies the ghost block's `Foreground`
  whenever that property changes.

## Decisions

- **The copy stays, the staleness goes.** Leaving the run to inherit was rejected: the original comment
  chose the explicit brush so the result does not depend on how Avalonia propagates Foreground into inlines.
- **Source-pin test.** The control cannot be built without a render backend, so the test pins the
  subscription and the re-apply.

## How to verify

1. Type a partial path in the File Explorer bar so a ghost suffix shows.
2. Press Ctrl+Shift+T to switch theme. The suffix recolors at once, without typing.
