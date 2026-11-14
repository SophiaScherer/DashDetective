# Shortcut capture leaves Tab to focus traversal

Work item 143.

## What was wrong

An armed Settings → Keyboard capture box accepted a bare Tab or Shift+Tab. Binding an action to either
claims Tab app-wide, so keyboard focus traversal stopped working. The shortcut catalog says Tab must keep
moving focus.

## What was built

- `CaptureKeys.Classify` takes the held modifiers and returns a new `Ignore` for Tab and Shift+Tab.
- The box does not mark an ignored key handled, so Tab moves focus as usual and the capture stands down
  through `LostFocus`.

## Decisions

- **Enter and Space stay capturable.** Tests pin them, and neither is focus traversal.
- **Ctrl+Tab and Alt+Tab-style chords still bind.** Only the two gestures focus traversal uses are refused.

## How to verify

1. Open Settings → Keyboard, click a shortcut's box, and press Tab. The capture ends, focus moves on, and
   the binding is unchanged.
2. Arm it again and press Shift+Tab: the same result.
3. Arm it and press Ctrl+Tab: it binds as before.
