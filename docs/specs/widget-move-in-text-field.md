# Leave Ctrl+Shift+arrow to a text field

Work item 98: *Ctrl+Shift+←/→ in a text field inside a widget moves the widget instead of selecting a word.*

## What was wrong

Keyboard reordering binds `Ctrl+Shift+←/→` to moving the widget or card the keyboard is on. Both
shortcuts kept the catalog's default `AllowInTextInput: true`, so they also fired while a text box had
focus. The window walks out from the focused control to the nearest reorderable panel, and a text box
inside a widget counts. Typing in the Network tab's Ping or DNS box and pressing `Ctrl+Shift+←` moved
the Ping widget and saved the new order. The selection did not change.

## What was built

- `MoveItemBack` and `MoveItemForward` are now `AllowInTextInput: false` in `ShortcutCatalog`, the
  guard `Back` and the `/` focus gestures already carry.
- `KeyboardReorderShortcutTests` pins that the gesture resolves to no move while a text box has focus,
  and that a rebound move keeps the guard.

## Decisions

- **The guard goes on the shortcut, not in `TryMoveFocusedItem`.** The catalog flag is the app's one
  rule for "this key belongs to the text box", and it survives a rebind for free, since a rebind copies
  the entry with new gestures.
- **A focused card still moves.** `KeyboardFocus.IsTextInputFocused` is true only for a `TextBox`, so a
  focused card, chevron or button inside a widget still reorders it.
- **No special path for a widget that holds a field.** The user Tabs off the field to the card or
  another control in it, then moves it. Taking the key from the field would break the platform's word
  selection, which is the bug.

## How to verify

1. Open Network and type a few words in the Ping box.
2. Press `Ctrl+Shift+←`. The selection grows by a word and the Ping widget stays put.
3. Press `Ctrl+Shift+→`. The selection shrinks or grows by a word; the widget stays put.
4. Press Tab until a card (not a text field) has the focus ring, then press `Ctrl+Shift+→`. The card
   moves one slot later.
5. Rebind "Move the focused widget or card one slot earlier" in Settings → Keyboard, return to the Ping
   box and press the new keys. The widget stays put.
6. Restart the app. Only the move from step 4 was saved.
