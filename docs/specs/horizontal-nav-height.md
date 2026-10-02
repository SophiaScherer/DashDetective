# Horizontal navigation bar height

Work item 50, Sprint 3 — *Too much white space when the toolbar is docked top or bottom.*

## What was built

A navigation bar docked to the top or bottom edge is now exactly as tall as its content, at every
interface and text size. It used to be a fixed 64px (54px collapsed) multiplied by the text scale.

## Why it had white space

`NavigationViewModel.RailThickness` grew the bar with the text scale on both axes. That is right for a
vertical rail, whose *width* has to hold the growing labels. On a horizontal bar only the label *line*
grows; the 18px icons, the 32px avatar and the 30px logo do not. So at 150% text the bar was ~96px
tall around ~50px of content, and at 200% ~128px around ~60px. That surplus was the empty band.

## Key decisions

- **No `Height` on a horizontal bar.** It sizes to its tallest part, so there is no number to drift
  out of step with the content. `RailHeight` is gone; `RailWidth` is the only fixed dimension.
- **The horizontal footer drops to `14,6` padding** (from `14,12`), bringing it to the item rows'
  44px. Otherwise the avatar block (56px) would set the height and leave a band above and below the
  items. The padding moved from an inline attribute into the `Border.rail Border.footer` styles, since a
  local value would have beaten the horizontal override.
- **No collapse tween on a horizontal bar.** There is no fixed height to interpolate, and collapsing a
  horizontal bar only hides the labels beside the icons — it barely changes the height.
- **The drag preview uses the last measured height.** The view reports the rail's height from
  `SizeChanged` (`ReportBarHeight`), and `RailThickness(horizontal: true)` returns it. Before the bar
  has been horizontal at the current text size, it returns `HorizontalBarEstimate` (45px, the height
  at 100%). A text-scale or collapse change made while the bar is vertical discards the measurement;
  a horizontal bar reports its own new height. This is the one place the preview can be a few pixels
  off: the first drag to a horizontal edge after one of those changes.
- **The drop band is multiplied by the interface size.** The rail is drawn inside the scale host and
  the band in the window's overlay, outside it, so at 150% the band used to be two-thirds of the bar
  on either axis. This predates this change but broke step 5 below.

## How to verify

1. Settings → Appearance → Navigation → Position: **Top**. The bar should hug its items: no band of
   empty space above or below the row of items, the brand logo or the avatar.
2. Repeat at **Bottom**.
3. For each edge, set Settings → Accessibility interface size and text size to 80%, 100%, 150% and
   200%. The bar should grow only as much as the labels do. Nothing should clip: labels, the selected
   item's underline, the avatar and the Help button.
4. Collapse the bar (the footer toggle, Ctrl+B or Settings). The labels hide, and the height stays the same or shrinks
   slightly.
5. Drag the brand logo toward the top edge while docked left. The accent drop band should match the
   height the bar lands at.
6. Narrow the window until the items overflow. A horizontal scroll bar appears, and the bar grows by
   its height rather than covering the items.
