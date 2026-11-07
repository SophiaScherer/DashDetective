# Dashboard card reorder (work item 15)

The Dashboard's stat cards can be dragged to reorder, within their own row only. The cards and the chart
widgets below them are two separate flows: a card cannot be dragged into the charts, and a chart cannot be
dropped into the card row. This replaces the item's original acceptance criteria, which asked for one
flow mixing cards and charts.

The decisions are recorded in [FEATURES.md → Dashboard](../FEATURES.md#dashboard) and
[Drag to reorder](../FEATURES.md#drag-to-reorder); the files are in [SOURCE-MAP.md](../SOURCE-MAP.md)
under `src/Shared/Layout` and `src/Tabs/Dashboard`. It builds on the slide animation from
[widget-drag-animation.md](widget-drag-animation.md).

## What was built

| Piece | Where | What it does |
| --- | --- | --- |
| The card row | `src/Tabs/Dashboard/DashboardView.axaml` | A `UniformFlowPanel` with `DragGrip="Marked"`, `ConfineDrag="True"` and its own `Order`; each card's `DashboardLink` is marked `Reorder.IsGrip`. A Dashboard-local style gives the held card the strip's lifted look. |
| `CardOrder` | `src/Tabs/Dashboard/DashboardViewModel.cs` | A second `SavedOrder`, `dashboard.cards`, beside the board's `dashboard`. The shell persists, restores and resets it with every other order. |
| `DashboardCard.WidgetId` | `src/Tabs/Dashboard/DashboardCard.cs` | `dashboard.card.{device id}`; a GPU uses its label ("GPU", or "GPU 0" once there are several), since its device id carries a per-boot LUID. |
| Fixed point | `src/Shared/Layout/ChildOrder.cs` | `Move` never moves a child with no id and never lets another cross it. The card row has no id. |
| `ConfineDrag` | `src/Shared/Layout/ReorderablePanel.cs`, `ReorderDrag.cs`, `WidgetBoardLayout.Confine` | Keeps the held item drawn inside its panel. |
| Stale press | `src/Shared/Layout/ReorderDrag.cs` | A pending press is dropped on `Detach` and on a move with no button held. |
| Tab stops | `src/Shared/Layout/ReorderablePanel.cs` | At arrange, a generated item whose root is already a focusable control is not made a second tab stop. |

## Decisions

- **Two flows, not one.** The card row is a strip of its own and the board's other children are its
  widgets. A strip's items are the panel's grandchildren, so they cannot join the board's order anyway;
  the fixed-point rule stops the board from moving the row, from a drag or from a keyboard move falling
  through from the row's last card.
- **The link is the grip.** A card is a button, and `ReorderGrip.Item` refuses a press on one. Under
  `Marked`, the drag captures only past the threshold, which cancels the button's press, so a still press
  and a double click are the link's as before.
- **Confined, not merely refused.** A card held over the charts could never land there, so it is kept in
  the row rather than drawn over them.
- **A GPU card goes by its label.** A LUID changes every boot; the label is stable while the GPU count is.
  When the count changes the labels change, and those cards return to their declared place.
- **One tab stop per card.** Every reorderable item is made a tab stop, but a generated item that is
  itself a focusable control already is one; its container as a second stop took focus and did nothing.
  The same rule removes the duplicate stop on the Performance rail's rows. It is decided at arrange,
  because a generated container has no child yet when measure collects it.

## Known gaps

- While a card or widget is dragged, the tooltip of whatever is under the pointer can appear.
- A press that moves and is released within the double-click time can still open Performance on the
  following press; that comes from the double-click gesture itself, not from the drag.

## Manual verification

Checked on Windows 11 at 125 % display scaling by driving the app with synthesized input. A ticked item
passed; an unticked item was not checked. Back up `%AppData%/DashDetective/settings.json` first.

- [x] Drag a card along the row: neighbors slide, the card lands and the order is saved under
      `dashboard.cards`.
- [x] Pull a card down toward the charts: it stays in the row, and the order is unchanged after release.
- [x] Drag a chart's header up over the card row: the drop stays among the charts.
- [x] Focus a card and press `Ctrl+Shift+→`: it moves one slot. Past the row's end, nothing moves.
- [x] Single-click a card: nothing happens. Double-click it: its device opens in Performance.
- [x] Come back to the Dashboard and move the pointer over the cards: no drag starts.
- [x] Settings → Layout → Reset widget placements: the cards and charts return to their declared order.
- [x] Restart the app: a saved card order returns, GPU cards included.
- [x] Tab through the cards: one stop per card. The Performance rail: one stop per row, and
      `Ctrl+Shift+→` still moves a row.
- [x] `Ctrl+Shift+→` still reorders Hardware cards and Network and Storage widgets.
- [ ] Reduce motion on: a dropped card lands without sliding. *(Covered by the shared
      `ReorderablePanel` tests; not checked on screen.)*
