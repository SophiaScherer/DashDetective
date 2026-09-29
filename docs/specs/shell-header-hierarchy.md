# Shell header hierarchy

Work item 43: differentiate tab headers from universal headers.

## Problem

The toolbar's page name (the shell's header) was 15px SemiBold, and a widget's heading (`panelTitle`,
the page's header) was 13.5px SemiBold. Same weight, 1.5px apart: a heading on the page could not be
told from the shell around it.

## What was built

One new class in `src/Shared/Styles/SharedStyles.axaml`, declared beside `panelTitle`:

| Class | Where | Size | Weight | Color |
| --- | --- | --- | --- | --- |
| `shellTitle` | Toolbar page name | `TextSizeDisplay` (18) | Bold | `TextStrong` |
| `panelTitle` | Widget header (unchanged) | `TextSizeSubhead` (13.5) | SemiBold | inherited |
| `cardSub` | Both subtitles (existing) | `TextSizeCaption` (11) | Regular | `TextSubtle` |

`MainWindow.axaml`'s toolbar title takes `shellTitle` and its subtitle `cardSub`, with the inline
`FontSize`, `FontWeight` and `Foreground` removed. A local setter outranks a shared class, so leaving
them in would have silently undone the change.

## Decisions

- **Size and weight both.** 18 against 13.5 is a 1.33x step, and Bold against SemiBold makes the weight
  visibly different too. Neither alone was enough at these sizes.
- **Defined once.** The two title classes sit together in SharedStyles.axaml, so the gap between them
  lives in one file. `shellTitle` is used by the shell alone, which would normally keep it local to
  MainWindow. It is shared because the criterion is the relationship between the two classes.
- **Only the title carries the rank.** The toolbar subtitle already matched a widget's subtitle, so it
  now takes the same `cardSub` class instead of restating its setters.
- **`panelTitle` is unchanged.** Changing it would restyle every widget in the app for a problem that
  sits in the toolbar.
- **No tab view was touched.** Surveyed: `panelTitle` (the WidgetPanel template, Storage's Disk Activity
  header lead, the tray notice), Hardware's component cards (13.5 SemiBold, the widget level), Storage's
  drive cards and File Explorer's detail pane (14 SemiBold), Toolkit's and Help's uppercase section
  labels (11.5), and the modal titles (Help and Accent color at 16, End task at 15). The modals are
  above both levels and outside the page. None of the page headings copied the toolbar's look.
- **Theme and scale by construction.** Sizes come from the `TextSize*` ladder and the color is a
  `DynamicResource` defined in all four theme variants, so the rank holds in light, dark and high
  contrast and at every text size. Interface size transforms the whole tree, so it cannot change the
  ratio.

## Toolbar height

The toolbar row is `Auto` with `MinHeight="54"`. Inter's line height is about 1.21em, so the title
block (title, 1px spacing, subtitle) measures roughly:

| Text size | Before (15 + 11) | After (18 + 11) | Row |
| --- | --- | --- | --- |
| 80 % | 26 | 29 | 54 |
| 100 % | 32 | 36 | 54 |
| 150 % | 48 | 54 | 54 |
| 200 % | 64 | 71 | grows to about 71 |

At 200 % text the row already grew past 54 before this change. It now grows about 7px more. Nothing
clips, because the row is `Auto`. Interface size scales the 54px minimum with everything else. Width is
unchanged in practice: every subtitle is wider than its title, even at 18 Bold, and both still trim at
the window's minimum width.

## Tests

`tests/DashDetective.Tests/Shared/Styles/HeaderHierarchyTests.cs` pins:

- `shellTitle` is at least 1.25x `panelTitle` at 80 %, 100 % and 200 % text size, and strictly heavier.
- Both size from the `TextSize*` ladder, and `shellTitle`'s brush resolves in every theme variant.
- The toolbar has exactly one `shellTitle` and authors no local size, weight or color on it.
- No view under `src/Tabs` uses `shellTitle`.

## How to verify

The app was not run for this change, so check it by eye:

1. Open any page with widgets (Dashboard, Performance). The toolbar page name should be clearly larger
   and heavier than every widget heading below it.
2. Switch Theme to Light and back to Dark in Settings → Appearance, then turn on High contrast in
   Settings → Accessibility under each. The title should stay strong in every theme.
3. In Settings → Accessibility, set Interface size and Text size to 80 %, 100 % and 200 %, each
   separately and then together. The title and subtitle should never clip vertically. At 200 % text size
   the toolbar should grow taller, not cut the subtitle.
4. Narrow the window to its minimum. The title and subtitle should trim with an ellipsis, not push the
   search field or the toolbar buttons off.
