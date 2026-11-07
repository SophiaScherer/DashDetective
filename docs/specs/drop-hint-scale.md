# Scale the widget drop hint

Work item 108: *Widget drop hint drawn at the wrong size when interface size is not 100 %.*

## What was wrong

Dragging a widget to reorder it draws an accent band over the slot it will land in. That band lives in
the window's overlay layer, which sits outside the `ScaleHost` that applies the interface size.
`DragDropHint.ShowFrom` moved only the slot's top-left corner into the overlay and kept the slot's size
and the band's corner radius as they were inside the scaled page. At 150 % the band covered two thirds
of the slot, anchored at its top-left corner, with corners too tight for the card under it.

## What was built

- `ShowFrom` maps the whole slot through the transform from the board to the overlay, so position and
  size both scale.
- The corner radius scales by the same factor, so the band's rounding matches the card's.
- The mapping is a pure helper, `DragDropHint.Map`, tested without a window.

## Decisions

- **Only `ShowFrom` changed.** The nav bar's drag-to-dock calls `Show` with overlay coordinates it has
  already multiplied by the interface size, so it was correct and is left alone.
- **`ReorderDrag` is untouched.** It passes the slot in the board's own units, which is the right
  contract. The conversion belongs to the hint, which is the one piece that knows it draws outside the
  scale host.

## How to verify

1. Open Settings → Accessibility and set the interface size to 150 %.
2. On the Dashboard, drag a widget by its grip over another slot. The band covers the whole slot, and
   its corners follow the card's rounding.
3. Repeat at 100 % and 125 %. The band still fits the slot exactly.
4. Drag the navigation bar to another edge. Its band is unchanged from before.
