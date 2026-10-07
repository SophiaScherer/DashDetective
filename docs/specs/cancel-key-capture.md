# Cancel a shortcut capture

Work item 55, Sprint 4: *Allow cancel to stop choosing a key bind.*

## What was wrong

The Settings → Keyboard capture box already stood down on **Escape**, and it cancelled when it lost
focus. A **mouse user** had neither. Clicking empty page space takes no focus in Avalonia, so
`LostFocus` never fired, and nothing on screen offered a way out. The capture stayed armed until
something was assigned, which matches the item's report.

## What was built

- A **Cancel ×** button beside the capture box, visible only while it is armed. Its tooltip is
  "Cancel (Esc)", which also names it for screen readers.
- The key rule moved into `CaptureKeys.Classify` so it can be tested: a held modifier waits, Escape
  cancels, and any other key is captured.

## Decisions

- **The Cancel button is not focusable.** If it were, pressing it would move focus off the box first.
  `LostFocus` would then stand the capture down and hide the button before its click landed. Keyboard
  users already have Escape, which the tooltip names.
- **Escape can never be bound.** It cancels whatever modifiers are held with it, since it is the one
  guaranteed way out of a capture.
- **Nothing is written on cancel.** Both cancel paths stop the capture without raising
  `GestureCaptured`, so `SettingsViewModel.Rebind` never runs and `ShortcutOverrides` is untouched. The
  box's label goes back from "Press keys…" to the current binding.

## How to verify

1. Open Settings → Keyboard and click a shortcut's box. It reads "Press keys…" and a × appears beside it.
2. Click the ×. The box shows its old keys again, the × disappears, and the binding is unchanged.
3. Arm it again and press Escape: the same result.
4. Arm it, hold Ctrl and press Escape: the same result. No "Ctrl+Esc" binding is created.
5. Arm it and press a new key. It is bound as before, and the reset arrow appears.
6. Restart the app. Cancelled rows kept their bindings, meaning nothing was persisted by a cancel.
