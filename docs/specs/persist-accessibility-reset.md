# Save the Accessibility reset

Work item 107: *Accessibility "Restore defaults" is not saved when only scale, text scale or color vision changed.*

## What was wrong

Settings → Accessibility → **Restore defaults** resets every option on the accessibility service, and
the card's controls follow the service back to the defaults. The settings file is written only when
`SettingsViewModel.Changed` fires. The toggles raise it on the way back, because each has a change hook.
The interface scale, the text scale and the color-vision segments are written back without one. A
reset that moved only those changed the screen but saved nothing, so the old value returned on the
next launch.

## What was built

- `ResetAccessibility` raises `Changed` after the service is reset, so the shell saves the result.
- Three tests in `SettingsViewModelTests` pin the cases that went unsaved: text scale alone, interface
  scale alone, color vision alone.

## Decisions

- **The reset raises `Changed`, not the reflect.** Reflecting the service back runs at startup and after
  every edit too. Raising from there would save on every startup apply and duplicate each edit's save.
- **The shell's accessibility handler still does not save.** It is a layout handler (window floor, rail,
  clock width). Saving stays on `SettingsViewModel.Changed`, the one path every other Settings edit takes.
- **The raise is unconditional.** A reset with nothing to undo saves the same values again, which is
  harmless. The confirmation banner is already unconditional, so the two agree.

## How to verify

1. Open Settings → Accessibility and set **Text size** to 150 %.
2. Press **Restore defaults**. Text returns to 100 % and the green confirmation shows.
3. Close the app fully (Exit from the tray if tray mode is on) and start it again. Text is at 100 %.
4. Repeat with **Interface size** at 150 %, and again with a **Color vision** mode other than Off.
   Each comes back at its default after a restart.
5. Turn on **Reduce motion**, press **Restore defaults** and restart. It is off, as before.
