# Help modal: keep keyboard and pointer input inside it

Work item 86. The accent picker modal copied the Help overlay's shape and fixed three gaps in it along
the way. This change carries those fixes back to Help, plus one the picker gets elsewhere (handing focus
back on close).

## What changed

| Gap | Before | After |
| --- | --- | --- |
| Enter | `HandleShortcut` swallowed every shortcut while Help was open, and Enter is the global `Activate` shortcut, so a focused button never saw it. Space worked. | The Help branch returns `false` for `ShortcutId.Activate`, exactly as the picker's branch does, so the key continues to the focused button. |
| Click inside the card | `OnScrimPointerPressed` tested `Card.IsVisualAncestorOf(source)`, which is false for the card itself, and an empty spot on the card reports the card as its source, so Help closed. | The test is `source == Card \|\| Card.IsVisualAncestorOf(source)`, extracted as `HelpOverlay.IsInsideCard` so it is unit-tested. |
| Tab | Focus could leave the card and reach the page behind the scrim. From Settings, the accent picker could then open on top of Help, and Esc closed the hidden Help first. | The card sets `KeyboardNavigation.TabNavigation="Cycle"`, and the × takes focus when Help opens, so Tab starts inside the card and cannot leave it. |

Files: `src/Shell/Help/HelpOverlay.axaml(.cs)`, the Help branch of `MainWindowViewModel.HandleShortcut`,
and `tests/DashDetective.Tests/Shell/Help/HelpOverlayTests.cs`.

## Decisions

- **Mirror the picker, don't invent.** Every fix is the picker's own mechanism: the same `Activate`
  fall-through, the same `source == Card` check, the same `TabNavigation="Cycle"`, and the same posted
  `Focus()` at `Loaded` priority on open.
- **Focus has to move in, not just cycle.** `Cycle` only keeps focus inside the card once it is there.
  Before this change, focus stayed on whatever held it behind the scrim (the nav bar's Help button, a
  Settings control), so Tab would have continued from there. Taking focus on open is what satisfies "no
  control behind the scrim can be reached by keyboard".
- **The × takes focus.** It is the first focusable element in the card, and pressing it (Enter or Space)
  is a sensible default action for a read-only dialog. The picker focuses its wheel because arrow keys
  are its main input; Help has no equivalent. The ring is not shown on open, matching the picker.
- **Closing hands focus back.** Otherwise focus would be stranded on a hidden button and a keyboard user
  would lose their place. This is the Help equivalent of the picker's opener taking focus back
  (`SettingsView.OnAccentPickerClosed`), and it uses the same `:focus-visible` check to decide whether to
  bring the ring back. Help has several openers (F1 anywhere, the nav bar button, a search hit), so it
  remembers whatever held focus rather than naming one opener. If that element is gone, hidden or
  disabled, focus is cleared with `FocusManager.Focus(null)`, the idiom `GhostCompletionBox.ReleaseFocus`
  already uses. If focus has already moved somewhere outside the modal by the time it closes, it is left
  where it is.
- **A search hit is the one case that restores nothing.** Picking a Help result dismisses and collapses
  the search field before Help opens, so the box is hidden when Help closes and focus is cleared instead.
  If Help was opened with F1 from an expanded search box, focus goes back to the box, which reopens its
  dropdown. That is where the user was, so it is left that way.
- **The helper stays on `HelpOverlay`.** The picker has the same inline check. Sharing one helper would
  mean editing the Settings folder, which this work item does not cover.

## Manual verification

The app was not run for this change; the build and format gates were. These are the checks to run:

1. Open Help with F1. Press Enter: Help closes (the × had focus).
2. Open Help, press Tab to reach a tab (for example Tips), and press Enter: that tab is selected and Help
   stays open. Space does the same.
3. Open Help and click an empty area of the card (between the header and the tab strip, or in the body's
   margin): Help stays open. Click the dimmed scrim outside the card: Help closes.
4. Open Help and press Tab repeatedly, then Shift+Tab repeatedly: focus cycles through the ×, the four
   tabs (and anything else focusable in the card) and never lands on the nav bar, toolbar or page.
5. Go to Settings, put keyboard focus on the Accent color swatch, press F1, then Tab and Enter as many
   times as you like: the accent picker never opens on top of Help. Esc closes Help.
6. Tab to the nav bar's Help button so its focus ring shows, press Enter to open Help, then Esc: focus and
   its ring are back on the Help button. Repeat with a mouse click on the Help button: after Esc the
   button has focus but no ring.
7. Type in universal search, pick a Help result, then press Esc: Help closes and nothing reopens the
   search dropdown.
