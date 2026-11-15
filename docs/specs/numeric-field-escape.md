# Escape abandons a Settings number edit

Work item 121.

## What was wrong

Escape in a Settings number field (an alert threshold, a duration) is meant to abandon the edit. While an
alert or notice banner was showing it did not: the shell's tunnelling Escape handler dismissed the banner
and marked the key handled first, so the field's bubbling `KeyDown` never ran. `Capture` had already
stored every digit typed, so the half-typed value was kept and persisted.

## What was built

- `NumericField` hears Escape on a tunnelling `KeyDown` registered with `handledEventsToo`, restores the
  value from before the edit and releases focus.
- Enter stays on the bubbling handler, since nothing upstream consumes it in a text box.

## Decisions

- **The banner is still dismissed by the same keypress.** Both are dismissals and the shell handler is
  untouched. Changing it would affect Escape in every text box, including those inside Help and the accent
  picker, where Escape must keep closing the dialog.
- **No banner showing, no change.** The field handled Escape before; it now does so on a path that also
  survives an upstream handler.

## How to verify

1. Trigger a resource alert or a Settings confirmation so a banner shows.
2. Open Settings, click an alert threshold box, and change its number.
3. Press Escape. The box returns to the number it held before the edit and loses focus, and the banner is
   dismissed.
4. Repeat with no banner showing: the same result.
