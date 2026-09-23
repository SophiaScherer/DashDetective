# Custom title bar (work item 51)

The window used to wear the default title bar. It now draws its own on Windows: a slim strip with the
app mark and the window title, on the shell's sidebar surface, with caption buttons drawn from the app's
palette. Windows keeps owning every gesture on it.

The decisions are recorded in [FEATURES.md → Window chrome](../FEATURES.md#window-chrome); the files are
in [SOURCE-MAP.md](../SOURCE-MAP.md) under `src/Shared/Controls` and `src/Shared/Styles`.

## What was built

| Piece | Where | What it does |
| --- | --- | --- |
| `WindowChrome.Custom` | `src/Shared/Controls/WindowChrome.cs` | Attached to a `Window`. Sets `ExtendClientAreaToDecorationsHint` on Windows only, and not under a Windows contrast theme; re-decides on `ColorValuesChanged`; closes on Alt+F4 while extended. |
| `TitleBar` | `src/Shared/Controls/TitleBar.axaml(.cs)` | The strip. Marked `ElementRole=TitleBar`, sized from the window's `WindowDecorationMargin`, hidden whenever the window is not extended. |
| `TitleBarRules` | `src/Shared/Controls/TitleBarRules.cs` | The pure part: the platform decision, the geometry, Windows 11's caption metrics (46 × 32). Unit-tested. |
| `AppWindowDecorations` | `src/Shared/Styles/WindowChrome.axaml` | The caption-button theme: minimize, maximize/restore, close, with their `PART_` names and roles. Palette colors only; close hover is Windows' own red (`CaptionCloseHover` in `Palette.axaml`). |
| `MainWindow` | `src/Shell/MainWindow.axaml` | Opts in and docks the `TitleBar` on top, outside the `ScaleHost`. |

A second window (work item 45) opts in with the same three lines: `WindowDecorationsTheme`,
`WindowChrome.Custom` and a `TitleBar` at the top of its content, outside its own `ScaleHost`.

## Decisions

- **The OS owns the caption.** The bar answers `WM_NCHITTEST` with `HTCAPTION` through Avalonia's
  element role, so `DefWindowProc` runs the move loop, snap, the snap zones and double-click. No
  `BeginMoveDrag`.
- **The caption buttons are Avalonia-drawn, not DWM's.** Avalonia 12.1.2 offers no DWM-button mode any
  more. They are hit-tested as `HTMINBUTTON` / `HTMAXBUTTON` / `HTCLOSE`, which is what Windows needs for
  native behavior, the snap flyout included. Getting DWM's own buttons back would mean extending the DWM
  frame behind Avalonia's back, which Avalonia overwrites on every state change — not attempted.
- **Contrast themes get the native caption.** Under a Windows contrast theme the window stops extending,
  and the OS draws the whole caption in the user's contrast colors. The app's own high-contrast variants
  keep the custom bar, drawn from the palette.
- **Outside the `ScaleHost`.** The buttons are drawn at the OS's scale; the bar matches them rather than
  the interface size. Its text follows text size, and the bar grows for it.
- **Linux and macOS are untouched.** The hint is never set there and the bar hides itself.
- **Out of scope:** merging the toolbar into the title bar (Edge / Terminal style). A possible follow-up.

## Manual verification

None of this was observed while it was built — the app was not run. Check each on Windows 11 unless the
line says otherwise. Back up `%AppData%/DashDetective/settings.json` first if you change settings.

### Drag and snap
- [ ] Drag the bar: the window moves, with the OS's own drag (no lag behind the pointer).
- [ ] Drag the bar to the top edge of the screen: the maximize preview shows; release maximizes.
- [ ] Drag to the left and right edges: half-screen snap previews and snaps. Snap Assist offers the other
      windows afterwards.
- [ ] Drag to each of the four corners: quarter-screen snap.
- [ ] Windows 11: drag towards the top centre: the snap-layouts drop zone appears.
- [ ] Drag a maximized window's bar down: it restores under the pointer and follows it.
- [ ] Win+← / Win+→ snap left and right; Win+↑ maximizes; Win+↓ restores, then minimizes.
- [ ] The top edge above the bar (the first few pixels) still resizes the window; the side and bottom
      edges and all corners resize.

### Double-click
- [ ] Double-click the empty part of the bar: maximizes. Again: restores.
- [ ] Double-click the title text and the app mark: same (they are part of the caption).

### Caption buttons
- [ ] Hover the maximize button: after a moment the Windows 11 snap-layouts flyout appears; picking a
      layout places the window.
- [ ] Minimize, maximize, restore (the glyph switches to the double square when maximized), close. Close
      honors "Show in system tray" exactly as the old title bar's close did.
- [ ] Hover each: a light overlay; close turns red with a white glyph. Pressing darkens.
- [ ] Maximized: moving the pointer to the top-right corner of the screen lands on close.
- [ ] Tab through the window: focus never lands on a caption button.
- [ ] Alt+F4 closes (to the tray when that setting is on). Alt+Space opens the system menu (its Close is
      greyed — Avalonia's doing, known). Right-click on the bar opens the system menu.
- [ ] Accessibility Insights / Inspect: the three buttons are named Minimize, Maximize, Close.

### High contrast
- [ ] Settings → Accessibility → High contrast, in Dark and in Light: the bar is flat black / white with a
      solid hairline; the caption glyphs are pure white / black; hover and press are visible.
- [ ] Windows Settings → Accessibility → Contrast themes → Aquatic (and Desert): the custom bar disappears
      and the native Windows caption returns in the contrast colors, with native buttons. Switching the
      contrast theme off brings the custom bar back **without restarting the app**.
- [ ] Dark and Light themes, and a custom accent: the app mark follows the accent; nothing else does.

### Interface and text size
- [ ] Interface size 80 %, 100 %, 200 %: the bar stays 32 px and lined up with the caption buttons; the
      content below scales.
- [ ] Text size 200 %: the title grows and the bar grows with it; nothing is clipped; the buttons stay at
      the top.
- [ ] Display scaling 125 % / 150 % (Windows setting): bar and buttons still line up, glyphs are crisp
      enough.

### Navigation bar
- [ ] Dock the nav bar Top (right-click → Dock navigation → Top): it sits below the title bar.
- [ ] Right-click the nav bar: the dock menu still opens. Drag the brand area to each edge: re-docks.
      (Known: the drop band for Top is drawn over the title bar rather than beneath it.)
- [ ] With Help or the accent picker open, the title bar is not dimmed and the caption buttons work.

### Linux
- [ ] Ubuntu VM: the window has the desktop's normal title bar, no custom strip, and moves, maximizes and
      closes as before.
