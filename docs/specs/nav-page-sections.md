# Page sections from the nav bar

Work item 46, *Per-page navigation: nav bar dropdown that scrolls to a section.*

## What was built

- Pressing the current page's nav item again opens an "On this page" list of the page's sections. Choosing one scrolls it to the top of its scroller and flashes it.
- A caret on the selected item marks a page that has sections. A page with fewer than two shows no caret and opens no list.
- `PageSections` (`src/Shell/Navigation`) decides which widgets count and in what order. `MainWindow.RefreshSections` reads the current page's visual tree on every page change and every open, and hands it measured candidates.

## Decisions

- **Derived, not maintained.** A section is a visible, titled, outermost `WidgetPanel`, so no page keeps its own list.
- **Hardware's cards are marked, not rebuilt.** They are bordered cards with their own icon-tile header rather than `WidgetPanel`s, so they carry a `pageSection` class instead, and the reader titles a marked card by its automation name. They also take `revealFlash`, so a pick tints them. A machine with two GPUs lists Graphics twice, in page order.
- **Reading order of where widgets are arranged**, not markup order: rows top to bottom, then left to right within a row, because widgets can be reordered by dragging.
- **The nav item itself is the trigger.** A separate caret button would nest in or overlap the item's own button, add a tab stop to every page, and have no room on a horizontal bar. The item's press is `Activate`. `Navigate` is still what `Ctrl+1…9`, search and the cross-page jumps call, so landing on a page never opens its list.
- **Keyboard.** Tab to the selected item and press Enter: the first section takes focus, and Esc closes the list.
- **The popup follows the Processes options menu.** It has its own `ScaleHost`, is not light-dismissed so the window stays hoverable, and an outside press closes it.

## How to verify

1. `dotnet test`: `PageSectionsTests` covers the section rules and ordering, the `pageSection` marker and the Hardware card carrying it, and `NavigationSectionsTests` covers `Activate` vs `Navigate`, the list clearing on a page change, and the fewer-than-two rule.
2. In the app, open Settings and click Settings in the nav bar again: the list shows Appearance through Export & Data. Choose one, and the page scrolls to it.
3. Open Utilities and click it again: no caret, no list.
4. Open Hardware and click it again: the list shows Processor through Sensors. Choose Storage Devices, and the page scrolls and tints that card.
5. Drag a Dashboard widget to a new slot, then open the Dashboard's list: the order matches the new layout.
