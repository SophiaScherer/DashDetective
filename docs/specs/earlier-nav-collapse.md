# Earlier switch to the icons-only rail

Work item 48, Sprint 3, *Switch to small icons earlier when shrinking the window.* It follows work item
47, which replaced the collapse puck with a footer toggle, and is stacked on that branch.

## What was built

The navigation bar folds to icons at a single derived breakpoint, `NavigationViewModel.AutoCollapseThreshold`,
instead of a flat 820px:

| Bar | Folds when the window is narrower than | At 100% interface and text |
| --- | --- | --- |
| Vertical rail | (expanded rail width at the current text size + `MinPageWidth`) × interface size | 236 + 772 = **1008px** (was 820) |
| Horizontal bar | (the width its labeled items need, measured) × interface size | whatever the labels need, typically ~1250px |

## Key decisions

- **1008px is Fluent's number.** It is the width at which WinUI's NavigationView leaves its expanded
  mode, so the rail now folds where Windows' own apps do. `MinPageWidth` (772) is derived from it rather
  than chosen separately. This is a judgment call: if the rail folds too early now, lower `MinPageWidth`.
- **The breakpoint counts text size.** The old one didn't, so at 200% text the 472px rail kept its labels
  until the page was down to about 350px. That is the "cramped before the rail adapts" the item describes.
- **A horizontal bar's breakpoint is measured.** Its labels overflow long before 1008px: nine labeled items
  need roughly 1250px. The view reports the bar width minus the item strip's viewport plus the strip's
  extent from `ScrollChanged`, and the bar folds when the window is narrower than that. This is what
  "labels never clip before the switch" means for that orientation. A vertical rail's labels cannot clip,
  because the rail's width already grows with the text.
- **The measurement only exists while the labels show.** Until an expanded horizontal bar has been laid
  out, and after any text-size change, the vertical breakpoint stands in. That can't cause oscillation:
  once measured, the fold point is stable, and a collapsed bar ignores reports.
- **One place.** Every re-test (width, interface size, text size, dock edge, a new measurement) goes
  through `UpdateAutoCollapse`, which reads `AutoCollapseThreshold`. The crossing rule is unchanged,
  so an explicit toggle still sticks until the window crosses back.

## How to verify

1. At 100% interface and text size, with the bar docked left, narrow the window slowly. The rail
   should fold to icons as the window passes ~1008px wide and unfold when it widens past it again.
2. Set text size to 200% and repeat. The rail should fold at ~1244px, not at 820px.
3. Set interface size to 200% (the largest). The fold point doubles, so on a 1920px-wide screen a
   maximized window shows the icons-only rail. Widening past the threshold restores labels.
4. Dock the bar top. Narrow the window: the bar should switch to icons at the point its labels would
   start to scroll out of view, with no horizontal scroll bar ever shown over labeled items.
5. Click the footer toggle while the window is narrow: it expands, and stays expanded until the window
   is widened past the fold point and narrowed again.
