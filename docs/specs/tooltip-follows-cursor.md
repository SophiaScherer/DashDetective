# Tooltips follow the cursor

Work items 16 and 69, Sprint 5: *Tooltip does not follow the cursor or dismiss on pointer movement* and *Dismiss and track the tooltip on pointer movement*.

## What was wrong

Avalonia places a pointer tooltip at the cursor once, when it opens, and then leaves it there. On a large owner such as a Dashboard stat card or chart, the tip stayed where the pointer had entered while the pointer moved on across the card. Moving to the next card handed the tip over correctly, but several cards share one tip text, so the new tip looked like the old one. Both together read as a tip that neither moves nor dismisses.

The framework already handled dismissal. It closes a tip as soon as a move takes the pointer off its owner, and after a layout or scroll change under a still pointer it re-checks the control there. An earlier app-side dismissal guard was reverted for that reason. The missing piece was tracking.

## What was built

- `ToolTipTracking` (`src/Shared/Controls`), attached to every top level by one style in `SharedStyles.axaml`. On each pointer move over the owner of an open tip, it re-places the tip at the cursor.
- The tip is moved by toggling the owner's `ToolTip.HorizontalOffset` by a sub-pixel `Nudge`. Avalonia re-reads the pointer whenever an open tip's offset changes, and that is the only public way to move one. The owner's own offsets are kept and restored exactly when the tip closes.
- A quick downward move can land the pointer on the tip itself, whose window then receives the moves. The owner is found through the tip's `Popup.PlacementTarget`, and the tip is shifted by the pointer's distance from its anchor, so it keeps following.

## Decisions

- **Tracking only, no dismissal code.** Closing is the framework's job, and duplicating it is what was reverted before.
- **Every tooltip follows, not only the widgets'.** A tip pinned where the pointer entered is the same defect on any owner. A tip placed against its owner (any `ToolTip.Placement` other than `Pointer`) stays put. No control in the app sets one today.
- **Reduce motion does not affect it.** Following the pointer is a direct response to input, not an animation, and re-placing an open tip does not re-run its fade.

## How to verify

Discriminate by owner, not by pixels. Several owners share a tip text, so a correct hand-off between two of them looks the same as a tip that never moved. On Windows each tip is its own top-level window: enumerate the app's windows (`EnumWindows`, filtered to its PID), and read each tip window's handle and its text through UI Automation. A handle lives for one opening.

1. Hover the Dashboard's CPU card until its tip opens. Move within the card: the tip window keeps its handle and moves with the cursor, about 20 DIP below it.
2. Move onto the CPU Utilization chart. A new handle opens, reading "Double-click to open CPU in Performance", at the cursor. Move within the chart, and it follows.
3. Flick the pointer down by more than 20 DIP so that it lands on the tip. The tip moves away from under the pointer and keeps following.
4. Move into the gap between widgets. No tip window remains.
5. Sweep fast across all five stat cards and stop in a gap. No tip window remains.
6. With a tip open on a card, scroll the page with the wheel without moving the mouse. The tip closes, and no tip window remains.
7. With a tip open, move the pointer outside the app's window. The tip closes.

All seven were checked against a Debug build on Windows 11 at 125% scaling. Each tip was identified by its window handle and its UI Automation text.
