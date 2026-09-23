# Navigation collapse toggle

Work item 47, Sprint 3: *Revisit the nav collapse button.* The item asks for a design decision
first: at least three alternatives, one chosen with its reasoning, and the half-puck removed unless it
wins on merit. The board was read-only overnight, so the alternatives are recorded here and in the PR
instead of on the work item.

## What it replaced

A half-disc "puck" on the bar's content-facing edge. It appeared only while the pointer was over the
bar, plus a 600 ms grace period after leaving. The problems:

- **Hidden.** Nothing showed it existed until the pointer was already over the bar. It was never a
  Tab stop, so on the bar itself collapse was mouse-only. `Ctrl+B` and Settings did work.
- **Overlapping.** On a horizontal bar it sat over the middle nav item. Once work item 50 made that
  bar content-height, it covered the item's lower 6px, including the selected underline.
- **Out of place.** The half-disc shape appears nowhere else in the app. It needed its own geometry
  (radius, per-edge corner rounding and alignment), a reveal timer and pointer tracking.

## Alternatives considered

| | Design | For | Against |
| --- | --- | --- | --- |
| **A** | **Caret button in the footer, beside Help** *(chosen)* | Always visible. A Tab stop. Uses the footer, which is already the bar's control cluster in all four orientations. Reuses `Button.navCtl` and the app's disclosure caret. Overlaps nothing. | Permanent chrome, which the puck existed to avoid. One more 30px button on a collapsed rail. |
| B | Toggle beside the logo ("hamburger", as in Fluent NavigationView and Windows Settings) | A familiar spot. Sits at the start of the bar. | The logo strip is the drag-to-dock handle, so a button there crowds the grab target. A collapsed rail would need to stack logo and button. This design was removed once already, in PR #46. |
| C | Full-length edge strip: a thin splitter-like band along the content edge that highlights on hover and toggles on click | No chrome at rest. Doesn't overlap items. A large target along one axis. | Still hover-only, so just as hard to find as the puck. Thin across the other axis. Not a Tab stop. Easy to confuse with a resize handle. |
| D | No on-bar control: `Ctrl+B`, Settings, plus a "Collapse" entry in the right-click menu | No chrome at all. | Undiscoverable for mouse users. Relies on knowing the shortcut or finding Settings. |

**Why A:**
- It is the only option that is visible, keyboard-reachable and clear of the items all at once.
- It reuses what the bar already has: the footer cluster, the `navCtl` button style and the `Icons.Caret*` disclosure glyph. Nothing new has to be drawn.
- The cost is real: the bar now carries permanent controls, where FEATURES.md used to say "no permanent control chrome". Two small buttons in a footer that already holds one is the smallest version of that cost.

## What was built

- A `Button.navCtl` holding a filled caret. It sits next to Help in a `StackPanel` that docks where
  Help used to (`ControlsDock`). The panel stacks as a column on a collapsed vertical rail
  (`ControlsOrientation`) and as a row everywhere else.
- The caret points the way the bar will move (`ChevronPointing`, unchanged from the puck). The
  tooltip is "Collapse navigation" or "Expand navigation" (`CollapseToolTip`). The shared Button style
  also uses the tooltip as the accessible name, so screen readers announce the action.
- Removed: the puck button and its styles, `ShowChevron` / `IsChevronVisible`, the puck geometry
  properties, the grace timer (also dropped from the view model's test-seam constructor) and the
  rail's pointer-enter/exit tracking.

## How to verify

1. Dock the bar to the left, right, top and bottom in turn. Each time, check that the toggle sits
   beside Help, its caret points toward the docked edge, and clicking it collapses the bar. The caret
   then points away from the edge, and clicking again expands it.
2. On a collapsed left or right rail, Help and the toggle stack vertically under the avatar and
   nothing clips.
3. Press Tab through the window. The toggle takes focus, draws the focus ring, and Space or Enter
   toggles the bar. `Ctrl+B` still works.
4. Hover the toggle. The tooltip says what a click will do and updates after each click. Narrow the
   window below the auto-collapse width: the rail folds, and the tooltip says "Expand navigation".
5. Check light, dark and both high-contrast themes, and interface sizes of 80%, 100% and 200%.
6. With a screen reader (Narrator), the toggle is announced as "Collapse navigation" or "Expand
   navigation".
