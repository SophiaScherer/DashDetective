# Close the search dropdown when focus leaves the box

Work item 100: *Tabbing out of the universal search box leaves the dropdown open and takes Up/Down app-wide.*

## What was wrong

The results dropdown is a popup that is deliberately not light-dismiss, because a light-dismiss popup
takes focus and the user could no longer type. An outside-click watcher stands in for light dismiss,
but it only hears pointer presses and is removed when the box loses focus. When the box lost focus, the
view only collapsed an idle field, which does nothing while the dropdown is open.

Pressing Tab out of the box therefore left the dropdown showing over every page. A later outside click
did not close it. The shell's active scope stayed `Search`, so Up and Down walked the hidden results
instead of scrolling the page. Only Esc got out.

## What was built

- `UniversalSearchViewModel.NotifyFocusLeft` closes the dropdown the way Esc does, keeping the term, and
  then collapses the field if it is empty.
- The view calls it from its posted `LostFocus` check whenever focus has landed somewhere other than
  the box or the popup.

## Decisions

- **Leaving keeps the term; clicking away drops it.** Tab is a step to the next control, closer to Esc
  than to abandoning the search, so Ctrl+F or a click brings the same results back. An outside click
  still runs `Cancel` as before.
- **Focus inside the popup does not count as leaving.** Pressing a result row moves focus into the
  popup before the row's tap opens it. Closing on that would close the dropdown under the click.
- **Tab with a ghost completion still accepts it.** That Tab never moves focus, so nothing here sees it.
- **The work item 66 guard is untouched.** Every close still goes through `CloseDropdown`, and the
  popup's `IsOpen` stays TwoWay.

## How to verify

1. Press Ctrl+F and type a term with no ghost completion, or one whose ghost is already accepted.
2. Press Tab or Shift+Tab. The dropdown closes, the term stays in the box, and focus moves on.
3. Press Down. The page scrolls or the focused control acts; no result list appears.
4. Click into the box. The results come back. Click a row: its page opens.
5. Press Ctrl+F on an empty box with recents showing, then Tab. The field collapses to its icon.
6. Press Ctrl+F, type a term, and press Esc. The dropdown closes and the term stays, as before.
7. Press Ctrl+F, type a term, and click empty page space. The box empties and collapses, as before.
