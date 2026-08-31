# DashDetective — Completed Features

The detailed write-up behind each shipped feature: what it does, and the decisions inside it that are
load-bearing. **Everything here is live** — this is reference, not a plan, and nothing in it is a
to-do.

Read [ARCHITECTURE.md](ARCHITECTURE.md) first for how the pieces fit together, and
[SOURCE-MAP.md](SOURCE-MAP.md) for what individual files do. The rules for changing any of this are in
[AGENTS.md](../AGENTS.md), which also lists the few things that are deliberately **not** built.

## Contents

- [Navigation bar (shell-level)](#navigation-bar-shell-level)
- [Dashboard](#dashboard)
- [Universal search](#universal-search)
- [Settings](#settings)
- [File Explorer](#file-explorer)
- [Network](#network)
- [Processes](#processes)
- [Performance](#performance)
- [Toolkit](#toolkit)
- [Keyboard shortcuts](#keyboard-shortcuts)
- [Storage](#storage)
- [Page lifecycle](#page-lifecycle)
- [Widget system](#widget-system)
- [Multi-GPU](#multi-gpu)
- [Repo-hygiene / portfolio pass](#repo-hygiene--portfolio-pass)
- [De-duplication / composition refactor](#de-duplication--composition-refactor)


## Navigation bar (shell-level)

The sidebar is a self-contained, **collapsible and dockable**
component — `NavigationView` + `NavigationViewModel` under `src/Shell/Navigation/`. The shell root
(`MainWindow.axaml`) is a `DockPanel` that hosts the bar via `DockPanel.Dock="{Binding Nav.Dock}"`,
so the user can dock it to any edge — **left, right, top, or bottom** — and **collapse it to an
icons-only rail**, in any orientation. The bar carries **no permanent control chrome**; every entry
point drives the **same shared** `NavigationViewModel`:
- **Collapse/expand** — a **semi-circular puck domed INTO the bar**, its flat side flush on the
  content-facing edge, revealed while the pointer is over the bar **and for a 600 ms grace period after
  it leaves**. It is a true half-disc: one radius deep, two long, both **inward** corners rounded by the
  full radius (no clamping). Its chevron points the way the bar will move (at the docked edge when
  expanded, away from it when collapsed). It is a sibling of the rail, not a child, so its rounding and
  alignment stay its own. **It used to stand outside the bar and that was the bug**: a hidden control is
  not hit-testable, so reaching for it left the rail, dropped `:pointerover`, and took the puck away
  mid-reach. Inside the bounds, reaching for it never leaves the rail. Two consequences: the view needs
  no `ClipToBounds` and the shell no `ZIndex` (both existed only to let it draw outside), and the
  reveal is a **bound flag, not a style** — `ShowChevron` (`IsChevronVisible && !IsDragging`), because a
  style setter cannot override a local `IsVisible` binding, so the drag rule had to move to the VM. The
  grace period is an `IUiTimer` on the `UniversalSearchViewModel` debounce shape (internal ctor +
  `FakeUiTimer`), which is what makes it testable headlessly.
- **Re-dock** — **right-click anywhere on the bar** for a "Dock navigation" menu at the pointer. The
  `ContextFlyout` is declared once on the rail `Border`: `ContextRequested` bubbles, so the brand, the
  items, the footer and any empty space all reach it.
- **Re-dock by drag** — press and drag the **brand area** to the nearest window edge. The bar **dims
  in place** for the gesture while an accent drop band and a cursor chip preview the target edge.
- **Motion — the bar is the ONE place in this app that animates**, and only for its own two moves. A
  **collapse tweens the rail's size** (~150ms, `CubicEaseOut`); the transition is declared **per axis**
  (`Border.rail:not(.horizontal)` → `Width`, `.horizontal` → `Height`) because `RailWidth`/`RailHeight`
  are `NaN` on whichever axis stretches. A **re-dock fades** — out 120ms, change edge, back in — because
  a `DockPanel` offers no path between edges to slide along. Every re-dock path (command, picker, drag)
  funnels through `BeginRelocate`, so none can skip it, and the move takes **two timer beats**: the first
  changes the edge while still faded out and with size transitions suspended by the `.relocating` class,
  the second fades back in. `.relocating` is declared **after** the drag dim so it wins the overlap — a
  dropped drag re-docks before `EndDrag` clears the dim. This is a deliberate exception to the app's
  otherwise instant styling, not a licence to animate elsewhere; the row-hover rule below still stands.
- **Settings → Appearance → Navigation** — Position + Collapse, both segmented controls.

The footer avatar shows the **device's own account picture** when the OS has one, read through the
`IUserPictureProvider` seam (`src/Services/Identity`) — the `AccountPicture\Users\{SID}` registry index
on Windows, `~/.face` / AccountsService on Linux. The reader returns encoded bytes rather than a decoded
image, so it holds no UI type; `NavigationViewModel` decodes once and falls back to the accent-gradient
**initials badge** whenever there is no picture, the read is denied, or the file will not decode. The
gradient stays the backdrop either way, so it still re-tints with the accent.

Orientation/collapse and every derived layout value (dock edge, rail thickness, item axis,
label/brand/footer visibility, accent-indicator bar↔underline, scroll axis, the puck's size /
alignment / rounding) are **computed properties on the VM — no value converters**. The rail
thickness has a **single owner**, `RailThickness(horizontal)`, which `RailWidth`/`RailHeight` delegate
to and the drop preview measures against; it takes the axis as an argument because a drag previews
edges the bar is not docked to yet. `MainWindowViewModel` owns page routing and delegates the bar to
`Nav`, wiring `Nav.SelectionChanged` → `CurrentPage`. Orientation and collapse **persist** (see
*Persistence* below); this is shared shell work, not a tab-local change.

- **Active Connections pager.** `« ‹ 1 2 3 4 › »` — the numbered `PageLink`s with **first/prev/next/last
arrows** bracketing them. The arrows are **stable `[RelayCommand]`s on the view model, deliberately NOT
`PageLinks` entries**: that collection is cleared and rebuilt on every 2.5s connections poll, so anything
living in it would be torn down twenty-four times a minute. All four route through the **same
`TryGoToPage`** the `Ctrl+←`/`Ctrl+→` shortcuts use, so keyboard and mouse cannot disagree, and page maths
stays in `PagerMath`. `HasPreviousPage`/`HasNextPage` drive `CanExecute` and are refreshed in
`RebuildPageLinks`; **the failure path must reset them by hand** (unlike `PageLinks`, stable commands
survive a `Clear()`, so an unavailable list would otherwise keep a live pager over nothing). Styled
`Button.pageArrow`, which copies File Explorer's `navBtn` disabled treatment — dim the glyph, keep the
surface transparent — because Fluent's default disabled state paints a filled box. No ellipsis: the
provider's 1000-row cap over a page size of 100 means at most ten numbers, which fit on one row.

- **Cross-tab jumps from Performance.** The Performance detail header carries a **"View in …" link** to the
tab that owns the selected device: a disk row to **Storage** (selecting that drive), the network row to
**Network** (flashing that adapter), and CPU/Memory/GPU to **Hardware** (tab only — it is a static spec
sheet with nothing to select). Built on the **`ToolkitViewModel.FileExplorerRevealRequested` shape**, and
that is the rule: `PerformanceViewModel` raises `StorageRevealRequested(int)` /
`NetworkRevealRequested(string)` / `HardwareRevealRequested`, naming a device and **nothing about which
tab shows it** — only `MainWindowViewModel` knows the wiring. Payloads are identities the destinations
already key on (the physical disk number, the adapter's friendly name), and the disk number match is exact
by construction: `DeviceInventory` names disk rows from the same `StorageComposer.Compose` the Storage
cards use. `ResourceRow` stays a data model — it carries a `ResourceLink` (label + command) built by the
view model, never routing of its own. **Both destinations load asynchronously from their constructors**, so
each needs the pending-slot treatment: `StorageViewModel._pendingReveal` is drained in `SelectDefaultDrive`
(outranking both the previous-disk and system-disk defaults), and `NetworkViewModel` re-raises after
`LoadAdaptersAsync`. A name or disk that matches nothing **degrades to a plain navigate**, never a failed
jump. The link is a new shared **`Button.link`** style (accent text + arrow) rather than a clickable title:
a bare title offers no resting cue that it goes anywhere.

## Dashboard

The **CPU, Memory, GPU, Storage and Network surfaces are live and functional**. CPU:
the CPU `StatCard`, the "CPU Utilization" panel, and the System Information **CPU** and **Cores**
rows. Memory: the Memory `StatCard`, the "Memory Utilization" panel, and the System
Information **RAM** row all read the real machine. GPU: one live GPU `StatCard` **per physical
adapter** (utilisation % + sparkline via PDH `\GPU Engine`, attributed by adapter LUID; adapters named
via DXGI in `GpuAdapterProvider`) and the System Information **GPU** row listing every adapter. GPU
**temperature** is now read live, but on the **Performance** tab only — appending it to this card's
caption is unbuilt Dashboard UI work (see *Deferred work* above).
Storage: the Storage `StatCard` shows live disk
**Active time %** (headline value + sparkline, both from PDH `\PhysicalDisk(_Total)\% Idle Time`
as `100 − idle`), with a system-drive capacity caption (`used / total` via `System.IO.DriveInfo`,
no WMI). Network: the Network `StatCard` and the "Network Throughput" panel show live download/upload
in **Mbps** (dual series on one shared scale + gradient fill, keyed by a `ChartLegend`) with a live
adapter-name caption, via `NetworkUsageSampler` (managed `System.Net.NetworkInformation`, no P/Invoke —
see the sampler note in *Folder Structure*). Every chart on the page states its own scale: the three
panels carry a caption, value labels, the time range and a cold-start line, and each card carries its
axis ends — see *Charting conventions* under *Theming* for which chart gets what, and why. System Information: the whole panel now reads the real machine — **OS** edition +
feature update (WMI `Win32_OperatingSystem.Caption` + registry `DisplayVersion`), **Device**
(`Environment.MachineName`), **BIOS** (`Win32_BIOS`), **Motherboard** (`Win32_BaseBoard`), **Build**
(registry `CurrentBuild` + `UBR`), and a live-updating **Uptime** (`Environment.TickCount64` on a 30 s
timer) — with the static facts loaded once at startup by `SystemInfoProvider` (WMI + registry, async);
the old "Updated N min ago" label was removed. **With this, every surface on the Dashboard page is now
live — nothing on it is static mock** (Settings is now partly live — see the Settings bullet). The shell **toolbar**
(top-right) is also fully wired: a live 24-hour **clock** (`MainWindowViewModel` 1 s `DispatcherTimer`;
its `TextBlock` has a fixed `Width` + centred text so the proportional-font `HH:mm:ss` reserves constant
space and ticking never reflows the toolbar),
a **Live** pill that pauses/resumes all sampling (`DashboardViewModel.SetLive`), a **Refresh** button
that now refreshes **whichever page is active** through the `IRefreshablePage` marker interface
(`src/Shared`) — on the Dashboard it forces an immediate re-read of every metric + static provider
(`DashboardViewModel.RefreshNow`), on the File Explorer it reloads the current folder, and pages that
don't implement it (Settings) simply ignore it (`MainWindowViewModel.Refresh` routes via
`CurrentPage as IRefreshablePage`) — and an **Export** button that saves a plain-text diagnostics report via the native file-save dialog
(`DashboardViewModel.BuildDiagnosticsReport`; the dialog is owned by `MainWindow.axaml.cs` since it
needs the window's `TopLevel`). Export uses the in-box `Avalonia.Platform.Storage` picker — no new
package.

## Universal search

**Fully live**. The toolbar box (`Ctrl+F`) searches
six categories at once and navigates to whatever is picked, revealing it in place.
- **Structure** (`src/Shell/Search/`). `SearchRanker` scores a term against text in four tiers kept
  200 apart (exact / prefix / word-start / anywhere) with a closeness bonus capped below 100, so a
  tier can never be crossed. `SearchAggregator` fans one query out to independent `ISearchProvider`s,
  merges and caps what comes back, and discards an answer whose term the user has already typed past.
  A provider that throws costs its own category and nothing else.
- **Providers.** Pages (over the live nav items), Settings (over `SettingCatalog`), Shortcuts (over
  `ShortcutBindings.HelpGroups`, so a result already knows its scope and the keys currently bound), Toolkit (over
  `ToolkitViewModel.AllEntries`, ranking the command text above its description), Processes (over the
  Processes tab's existing snapshot — no extra enumeration — folding a multi-process app into one
  row), and Files.
- **Jumping.** There is **no routing layer**. Each provider takes a "go there" callback built in
  `MainWindowViewModel` (the one class already holding every page), which navigates and then calls a
  small public `Reveal(...)` on the page. Settings rows carry their `SettingId` as their `Tag`, so
  the view finds the row to `BringIntoView()` and flash without a name-to-control switch; Toolkit
  rows do the same with the command string.
- **Revealing onto a page that isn't built yet (gotcha).** The shell navigates and reveals in one
  breath, so a page hosted in the **bounded self-scrolling `ContentControl`** does not exist when
  `Reveal(...)` is called — its child is built on the next layout pass, and an event raised there and
  then reaches no subscriber. Settings is unaffected (it sits in the scrolling host), which is why
  the plain event seam works there. `ToolkitViewModel` therefore **stores the pending reveal** and
  only nudges the event; the view drains it both when it attaches and when the event fires, so
  whichever happens first wins. Copy that shape for any future `ISelfScrollingPage` reveal.
- **File search** (`src/Services/Search/`). Prefers the Windows index (`Search.CollatorDSO` via
  `System.Data.OleDb`), falling back to a capped, cancellable breadth-first scan. `IFileSearch`
  separates *cannot answer* (null) from *found nothing* (empty) — only the former falls back. The
  term is inlined into the SQL because the provider will not bind parameters inside `CONTAINS`, so
  `SearchTermEscaper` owns that escaping alone and is tested apart from the query it feeds.
- **Completion.** `GhostCompletionBox` (`src/Shared/Controls/`) draws the rest of a suggestion after
  the caret for `Tab` to accept, used by the search box, the address bar and the process filter.
  `PrefixCompleter` is the shared rule: one match completes fully, several complete only as far as
  they agree.
- **Recents.** The last eight things opened, persisted through `AppSettings.RecentSearches` as one
  opaque string. Opening one re-runs the search and matches by identity, so an entry naming a deleted
  file or an exited process drops itself rather than promising something that no longer works.

## Settings

**Fully live**.
- **Appearance.** The **Theme** segmented control (Dark / Light / System) and the **Accent color**
  swatches are data-bound to `SettingsViewModel` and applied at runtime through a single
  `ThemeService` (see *Theming* below). The accent row's **first** swatch is a "Default"
  (multi-colour) option — a 2×2 four-colour square that restores the authored look (each dashboard
  graph its own colour, highlight blue); the four single-colour swatches recolour the highlight and
  hand the graphs a palette **derived** from that accent, each metric keeping a hue of its own.
  The **Clock format** segments (24-hour / 12-hour) are a `ClockFormatOption` on the `ThemeOption`
  pattern. The shell pushes the choice to the two places that show a wall-clock time — the toolbar
  clock and the Toolkit Execution Log — through `TimeOfDayFormatter` (`src/Shared`), the same way it
  pushes the NVIDIA opt-in to the GPU pages. **Display only, deliberately:** export file names, the
  report's `Generated:` line, the exported Toolkit transcript and `Log.cs` all stay 24-hour, so files
  remain sortable and machine-parseable. `ToolkitLogEntry` keeps its raw `DateTime` alongside the
  formatted string so rows already on screen re-stamp when the preference changes, and the toolbar
  clock's fixed width is sized for the wider 12-hour string so switching does not reflow the toolbar.
- **Monitoring.** The **Refresh interval** segments (0.5 / 1 / 2 / 5 s) are real `IntervalOption`
  selectable-item VMs (the `ThemeOption` pattern); selecting one calls
  `SystemMetricsService.SetInterval`, which retimes **only** the five 1 Hz metric channels — the
  coarse timers stay coarse (Dashboard uptime 30 s; Network adapters 5 s / connections 2.5 s /
  ping 2 s are NOT retimed). The three toggles are real templated `ToggleButton`s (shared
  `ToggleButton.toggle` style in `SharedStyles.axaml`, pixel-matching the old mock): **Resource
  alerts** (the master switch for the Alerts card below), **Show in system tray**, **Launch at startup**.
  **Launch at startup** writes the HKCU `…\Run` value via `IStartupRegistration`
  (`src/Services/Startup`, soft-failing).
- **Alerts.** Six rows — CPU, memory, GPU and disk-activity thresholds, a low-free-space threshold, and
  how long a breach must last — each a **typed whole number with its unit beside it** (`NumericField`),
  plus a per-row switch. Runs of preset segments were tried first and pushed the description text into a
  narrow column that truncated it; a threshold is also a number people want to state rather than pick
  from a shortlist. **The switch and the number are separate values on purpose**: the service encodes
  "not watched" as `0`, and a row storing only that would forget its threshold the moment it was switched
  off — so GPU ships *off, with 90 already in the box*, and re-enabling restores what was chosen. The
  shell folds the pair back into the zero-means-off contract, so the watcher never learns that a page has
  two controls. "Warn after" has no switch (it is the wait before a warning, not a warning) and reserves
  the switch column so every number still lines up. Every row is disabled on the whole `Border` while the
  master toggle is off, the `CanUseTray` idiom. `ResourceAlertWatcher`
  (`src/Services/SystemMetrics`) owns the logic, deliberately **outside** `SystemMetricsService`, which stays
  a pure fan-out of the three shared feeds: GPU and disk have no shared feed there because an aggregate
  across devices would report an average under one device's name. The watcher reads them anyway without
  breaking that rule — it owns its own samplers (their contracts require it) and takes the **worst device,
  named in the banner**. Three decisions worth keeping: the sustain window is **seconds converted against
  the live interval**, not a sample count (the old fixed 10 samples silently meant 5 s at the 0.5 s cadence
  and 50 s at the 5 s one); **GPU and disk activity default off**, because sustained saturation of either is
  what games, renders and large copies look like; and free space **skips unlettered volumes**, because
  Recovery/EFI partitions sit near-full by design and alerting on them is a banner that never clears. The
  shell shows the inline warning below the toolbar, with copy built from the breach (`×` dismisses the
  current one; a different resource taking over clears the dismissal).
- **System tray.** A `TrayIcon` declared in `App.axaml` (Show / Exit menu, wired in `App.axaml.cs`);
  with the setting on, closing the window hides to tray (`MainWindow.OnClosing`) instead of exiting.
  Real exit still runs the composition root's disposal.
- **Export & Data.** **Copy diagnostics** → clipboard (as text); **Export report** → the system
  snapshot as **text, JSON, Markdown, HTML or CSV**, picked in the native dialog's own type list; **Export
  CSV** → the rolling 60-sample metric histories, a different artifact from the report and so still its
  own button. The report is a `DiagnosticsReport` — sections of key/value rows — built by
  `MainWindowViewModel.BuildReportModel` from `DashboardViewModel.GetReportSections` plus the read-only
  accessors on Hardware, Network and Storage; each format is a renderer over that one model. Three
  decisions worth keeping: the **text format is pinned byte for byte by a test**, so an existing saved
  report and a new one still diff cleanly; the format comes from the **chosen filename**, not the picked
  filter, because Avalonia does not report the latter and a typed extension should win; and the HTML is
  **self-contained and exempt from the palette-ownership rule**, since a page opened in a browser has no
  access to the app's theme and must not look right only inside DashDetective. One `FileSave` helper owns
  the dialog for all of it, replacing three near-identical copies (toolbar Export, the two Settings
  buttons, the Toolkit log export).
- **Persistence.** All of the above (plus Appearance and Navigation) persist to
  `%AppData%/DashDetective/settings.json` via `SettingsStore` (`src/Services/Settings`; System.Text.Json
  source-gen, load-on-start with full soft-fail to defaults, debounced atomic save, `schemaVersion`).
  The composition root (`App` → `MainWindowViewModel`) applies a loaded snapshot through the seams and
  observes them to save; `ThemeService` stays the single theming applier — the store only observes.
  `TrayNoticeShown` rides along but is **not a preference** and has no Settings row: it is the record
  that the app has disclosed, once, that closing the window does not stop it.
  Theme, accent and the navigation choices **persist** through this rather than lasting a session.

## File Explorer

**Live and functional** (built in phases). A **read-only** three-pane
browser matching the design comp: a folder **tree** (left, drives-as-roots + lazily-loaded
subfolders), a **file list** (centre) with a clickable **breadcrumb**, **filter chips**
(All / Documents / Images / Archives), **sortable column headers**, and a **Show hidden** checkbox,
and a **details/preview** pane (right) showing Type / Size /
Modified / Created / Attributes / Location with **Open** and **Properties** actions. Data comes from
`System.IO` (`DriveInfo`/`DirectoryInfo`/`FileInfo`, lazy `Enumerate*` with
`EnumerationOptions{IgnoreInaccessible, AttributesToSkip=…}` — hidden/system entries are skipped by
default but shown when **Show hidden** is on, see below — per-entry soft-fail off
the UI thread); friendly type names via `SHGetFileInfo` (`SHGFI_TYPENAME | SHGFI_USEFILEATTRIBUTES`);
icons are **themed vector glyphs** with fixed per-type colours (no `HICON`→bitmap); Open via
`Process.Start(UseShellExecute)` (also on double-click); Properties via `SHObjectProperties` invoked
from the view code-behind (needs the window `TopLevel` handle, like Export). **No new dependencies**
(Owner/ACL field intentionally omitted). Tree/list selection uses a per-item `IsSelected` +
callback (the NavItem pattern), with the VM enforcing single selection.

Notable choices / deferred bits: this tab introduces the app's **first hierarchical control**
(`TreeView`) — an intentional, signed-off architecture addition. Tree roots are **drives**, not a
synthetic "This PC" node. Navigating via the list/breadcrumb does **not** sync the tree selection
(deferred by choice). Filter chips reuse the shared **segmented control** (`Border.seg`), not the
comp's softer chip; the details **preview** is a solid themed swatch, not the comp's literal
diagonal hatch. `TreeView` selection/hover colours are overridden to `AccentSoft`/`HoverOverlay`,
and the Fluent default hover is suppressed (it otherwise greys the whole ancestor chain, since a
`TreeViewItem`'s `:pointerover` is true when the pointer is over any descendant).

**Sorting, hidden files & contextual refresh (enhancement).** Three follow-on features, all kept
tab-local except the shared refresh seam:
- **Column sorting.** The `NAME / TYPE / MODIFIED / SIZE` headers are clickable (`Button.pick`
  cells bound to per-column `SortColumn` VMs — same selectable-item shape as `FilterOption`); the
  active column tints to `Accent` and shows a `↑`/`↓` arrow. Sorting lives in the **view model**, not
  the service: `FileItem`/`FileEntry` now carry the **raw** `long Size` (-1 for folders) and
  `DateTime Modified` keys alongside the display strings (the pre-formatted strings can't be ordered).
  `FileExplorerViewModel.RebuildVisibleEntries` (renamed from `ApplyFilter`) filters then `Compare`-sorts;
  `Compare` keeps **folders grouped above files** (grouping never inverts with direction), orders by the
  active `FileSortKey`, and breaks ties by name. Clicking a column flips its direction; a new column
  adopts an **Explorer-style default** (Name/Type ascending, Modified/Size descending).
- **Show hidden.** A themed `CheckBox` (in the **Options** flyout) bound to
  `FileExplorerViewModel.ShowHidden`. `DirectoryService` takes a `bool includeHidden` (picking
  between two `EnumerationOptions`); the tree threads it as a `Func<bool>` into each `FileSystemNode`
  so lazy expands honor the live setting. Toggling reloads the list **and** reconciles each loaded tree
  branch **in place** via `FileSystemNode.SyncChildrenAsync` — surviving folders keep their instance
  (so expansion and selection are preserved), newly-visible hidden folders are inserted and vanished
  ones removed, and an unexpanded node's chevron is kept honest without loading its subtree. (This
  replaced the earlier full `RootNodes.Clear()` rebuild that collapsed the whole tree on every toggle.)
  The checkbox style recolours the Fluent template parts (`Border#NormalRectangle`, `Path#CheckGlyph`)
  and is local to the view (the app's only checkbox — promote to `SharedStyles` if reused).
- **Contextual Refresh.** `FileExplorerViewModel` implements `IRefreshablePage` (`src/Shared`, the same
  marker-interface idea as `ISelfScrollingPage`); `Refresh()` re-reads the current folder via the
  existing `SetCurrentFolder`/`LoadEntriesAsync` path so the toolbar button picks up files added/removed
  on disk. See the toolbar note in the Dashboard bullet for the shell-side routing.
- **Live auto-refresh.** Since the app is read-only, changes come from the user's own filesystem, so the
  open folder updates itself without a manual refresh. `DirectoryWatcher` wraps a single
  `FileSystemWatcher` (one directory, non-recursive), coalesces the OS's event bursts with a ~300 ms
  debounce timer, and raises a UI-framework-agnostic `Changed` event; the VM holds one watcher,
  **re-points** it at the open folder in `SetCurrentFolder`, and on `Changed` hops to the UI thread
  (`Dispatcher.UIThread.Post`) into `ReloadCurrentFolderPreservingState`. That reload **keeps the
  selection by path** (the `_reselectPath` captured/consumed in `LoadEntriesAsync`, cleared only if the
  item is gone) and reconciles the matching tree node through the same `SyncChildrenAsync`, so new/removed
  subfolders (and their chevrons) show in the left tree too. It's a same-path reload, so the scroll
  position is kept (see below). The watcher is Windows-guarded and soft-failing (a vanished/denied path
  stays idle); the page is a never-disposed singleton, so the one watcher lives for the app's lifetime.
- **Scroll-to-top on navigation.** Navigating to a *different* folder resets the file list to the top;
  sort/filter/Refresh and auto-refresh of the *same* folder do not. The VM raises a `ScrollToTopRequested`
  event from `SetCurrentFolder` **only when the target path differs** from the current one; the view
  (which owns the named `FileListScroll` `ScrollViewer`) subscribes in `OnDataContextChanged` and calls
  `ScrollToHome()`.
- **Empty and error states.** Six situations used to render as the same blank pane, three of them as
  the literal words "This folder is empty": a protected folder, one that has vanished, a filter that
  hid everything, a folder holding only hidden entries, a genuinely empty folder, and the launch state
  before anything is open. `FolderMessages.Resolve` decides between them and returns a title + hint;
  the VM pushes the pair and the overlay renders it in the Toolkit empty state's shape. It is a
  **pure, render-free static** for the usual reason — `FileExplorerViewModel` reaches `FileTypeCatalog`
  and cannot be tested at all — so it follows `FileExplorerPanes` / `FileExplorerTableLayout`.
  Two things not to undo: the message is suppressed while a read is **in flight** (`_activeLoadId != 0`),
  not merely while `IsLoading` is set — that flag only rises after the 150 ms grace period, so gating on
  it flashes the wrong wording on every navigation; and `EndLoad` calls `UpdateFolderMessage()`
  explicitly, because a read fast enough never to raise `IsLoading` leaves that setter silent.
- **A bad typed path stays fixable.** `CommitPath` used to set `IsPathEditing = false` *before*
  validating, so a typo silently reverted and cost a full retype. The box now closes **only on success**;
  a file, a path that does not exist and one that will not resolve each set `PathError`, shown on a
  second row under the address field (orange, the `ActionMessage` / `formError` convention). Cleared by
  the next navigation and by the next keystroke. `Reveal` sets the same message rather than returning
  silently, so a stale universal-search hit is not a dead button. The error row uses a `Margin`, **not**
  the grid's `RowSpacing` — Avalonia applies row spacing even to a zero-height row, which would cost the
  bar 4 px permanently.

**Layout & scrolling (design rework).** The three panes are now **independently scrollable** and
**user-resizable**. Independent scrolling required a shell change: the page-host `ScrollViewer` in
`MainWindow.axaml` used to wrap *every* page, which left the panes unbounded in height so their own
scrollers never engaged (the whole tab scrolled as one). Pages that fill the viewport and manage
their own internal scrolling now implement the marker interface **`ISelfScrollingPage`**
(`src/Shared`); the shell hosts them **outside** the page-scrolling `ScrollViewer`, in a plain
`ContentControl` that the `*` grid row bounds to the viewport height (so the child is bounded and
each pane scrolls on its own). The page-host is a `Panel` with two mutually-exclusive
`ContentControl`s: the current page is routed to the scrolling host via
**`MainWindowViewModel.ScrollingPage`** or the bounded host via **`SelfScrollingPage`** (the other
is fed `null` so the view is only ever built once), toggled by **`CurrentPageSelfScrolls`**.
Dashboard/Settings scroll as a whole page (unchanged); `FileExplorerViewModel` is the only
self-scrolling implementer so far. (A `Disabled` `ScrollViewer` was tried first but does not
reliably bound its child, which clipped the bottom of long trees.) Resizing: the pane grid is *fixed · splitter · star · splitter · fixed* with two
`GridSplitter`s (shared style **`GridSplitter.paneSplitter`** in `SharedStyles.axaml`); side panels
default to **240** (left) / **300** (right) with the middle list as `*`, and each side column carries
`MinWidth`/`MaxWidth` (plus `MinWidth="320"` on the list) so drags clamp sanely and the list never
collapses at the window's 920 px minimum. Widths are **session-only — they reset to the defaults each
launch** (no persistence, by choice, like Theming). This tab deliberately touched the shell + shared
styles for the scroll seam; that's a cross-cutting concern (as Theming is), not a tab-local change.

**Large-folder responsiveness.** Opening `C:\Windows\System32` (5,033 entries) used to freeze the whole
app for **5.2 s**, and every sort click and filter chip paid it again. Two causes in the list itself and
two contributors found beside them, all fixed; the page now reports **zero** unresponsive samples through
a `System32` load, sort, filter or full-list scroll (measured with a `WM_NULL` `SendMessageTimeout`
round-trip, which fails only while the UI thread stops pumping).
- **The rows list virtualizes.** The rows `ItemsControl` carries a `VirtualizingStackPanel` items panel;
  without it every row realized a `Border`, a nested `Grid` (whose `GridColumns.Definitions` binding runs
  `ColumnDefinitions.Parse`), a `Path` and four `TextBlock`s up front. The named `FileListScroll`
  `ScrollViewer` **stays** — an `ItemsControl` has no scroll of its own, and the panel takes its viewport
  from `EffectiveViewportChanged`, which any ancestor scroller supplies.
- **`BulkObservableCollection<T>`** (tab-local) adds one `Reset(items)` that refills `Items` and raises a
  single `Reset` notification; a `Clear()` plus a per-item `Add()` was ~10,000 notifications on the UI
  thread for that folder. It backs **both** `VisibleEntries` and `FileSystemNode.Children`. Chosen over
  reassigning an `IReadOnlyList` property for two reasons: the tree still needs in-place
  `Insert`/`RemoveAt`, and a `Reset` clamps the `ScrollViewer` exactly as the old `Clear()` did, so scroll
  behavior is unchanged. **`SyncChildrenAsync` still merges and must never be turned into a reset** —
  it is what preserves node instances, expansion and selection across a "Show hidden" toggle or an
  auto-refresh.
- **Loading and empty states.** A navigation **clears the list up front** (the previous folder's rows
  outlived the breadcrumb by seconds, looking authoritative and staying clickable), then `IsLoading`
  drives a centered "Loading…" placeholder plus an indeterminate `ProgressBar.loadStrip` on the header
  hairline; `IsEmpty` says "This folder is empty" so a folder with nothing in it no longer looks like one
  still being read. The busy flag is **grace-gated at 150 ms** so ordinary folders never flash it, and
  `_activeLoadId` pairs with the existing `_pendingPath` guard: without it a load that finishes *before*
  the grace timer fires would have the timer latch busy back on with nothing left to clear it.
  `LoadEntriesAsync` takes `clearFirst` and `showBusy` so each caller says what it is — navigation clears
  and reports, the "Show hidden" toggle reports without clearing, and the watcher's auto-refresh does
  neither (nobody asked for it, and nothing is stale).
- **`ShellTypeNameCache`** memoizes the friendly type name **by extension** for one folder read. Both
  shells derive that name from the extension and attributes alone — Windows asks with
  `SHGFI_USEFILEATTRIBUTES`, which never opens the file — so one lookup answers every entry sharing an
  extension, and directories share a single answer. Measured on `System32`: the per-entry calls were
  **481 ms** of the read, which now completes in **30 ms**. Deliberately **per-read and unshared**:
  nothing to invalidate, nothing to synchronize.

## Network

**Live and functional** (built in phases). Matches the design comp's
Network page: six panels in two rows. The VM is constructed once in `MainWindowViewModel` and follows
the shared page lifecycle (it samples only while it is the visible tab — see *Lifecycle* below), reuses
the shared `Sparkline`, and adds **no new NuGet packages** (all
in-box: `System.Net.NetworkInformation`, `System.Net.NetworkInformation.Ping`, `System.Net.Dns`,
and `iphlpapi` P/Invoke). The `Network` `NavItem` (globe icon) sits between File Explorer and
Settings. Panels:
- **Adapters** — every adapter except loopback (physical + virtual), with a fixed-colour status dot
  (green connected / blue virtual / grey disconnected), status and link speed, via
  `AdapterInfoProvider` (managed `NetworkInterface`, async snapshot on a 5 s timer). The list is
  height-capped and scrolls so many adapters don't push the page down.
- **IP Configuration** — the primary adapter's IPv4 / mask / gateway / DNS / MAC / DHCP (monospace),
  from the same provider. Primary is chosen by `NetworkUsageSampler.SelectPrimary` (one source of truth).
- **Throughput** — live down/up **Mbps** as TWO stacked sparklines with **independent** dynamic
  scales (the comp's layout — unlike the Dashboard's single shared scale), via a second
  `NetworkUsageSampler` instance on a 1 Hz timer.
- **Active Connections** — netstat-style TCP+UDP table (Process · Remote · State · Protocol) with
  owning process names, on a 2.5 s timer. Windows reads the IPv4 `OWNER_PID` tables via feature-local
  `iphlpapi` P/Invoke (`GetExtendedTcpTable`/`GetExtendedUdpTable`); **Linux reads
  `/proc/net/{tcp,tcp6,udp,udp6}` and so includes IPv6**, attributing each socket by walking
  `/proc/[pid]/fd` for its inode. Rows are **keyed-diffed** in place (no flicker); de-duplicated by
  identity key in `ConnectionsProvider` (two UDP sockets can share PID+local endpoint, which would
  otherwise break the diff), sorted, **capped at 100** with an honest "N active · showing 100" caption.
  IPv6 endpoints are bracketed so the port is not mistaken for a hextet. PID→name is cached with
  stale-PID eviction and resolved per platform: Windows appends `.exe` and names 0/4 "System
  Idle"/"System"; Linux does neither (PID 4 there is a kernel thread) and shows "—" for a socket whose
  owner an unprivileged reader cannot see. Either way an unnameable process falls back to "PID n".
- **Ping** — **opt-in**, and the target defaults to **the machine's own gateway** (`NetworkGateway`,
  seeded off the UI thread and never over a value already typed; empty when there is no gateway).
  The panel's button is **Start/Stop**; Enter in the field applies the target and starts, because a
  text box that stops the monitor would surprise. Nothing sends until Start is pressed: this used to
  be a continuous 2 s ping to a hard-coded `8.8.8.8` begun in the **constructor**, so the app pinged a
  public resolver from launch — around 43,000 ICMP a day — whether or not the tab was ever opened,
  with nothing in the UI disclosing it. Still in-box `Ping`, 2 s timer, 1.5 s timeout, in-flight
  guarded; console-style last-3 replies + rolling avg-RTT / loss (`PingMonitor`). `ToolkitHostValidator`
  is deliberately **not** reused here — the value reaches managed `Ping.SendPingAsync` as a host
  string, with no process and no argument list, so there is no flag for it to become.
- **DNS Lookup** — **user-initiated**: one-shot resolve via in-box `Dns.GetHostEntryAsync` (3 s
  `CancellationTokenSource`), console-style output with record type (`DnsLookupProvider`). The field is
  seeded with `example.com` but **nothing resolves until Look up is pressed**, and Refresh re-resolves
  only once a lookup has been run — a manual refresh must not become the first packet the panel ever
  sent. It, too, used to fire from the constructor.
- **Lifecycle** — the tab is **no longer always-on**. `IActivatablePage` + `SamplingGate` start its
  timers when it becomes the visible tab and stop them when it stops being one; the ping monitor
  additionally waits for the user, and its on/off survives leaving the tab, so returning finds it as
  it was left. Measured: ~3 % of one core with the tab visible and pinging, ~0.2 % once another tab
  is selected.

Cross-cutting seams this tab added (both signed-off): the throughput sampler was **moved** from
`src/Tabs/Dashboard` to **`src/Services/Network`** (see *Folder Structure*) so Dashboard and Network
share it, and a new marker interface **`ILiveSamplingPage`** (`src/Shared`) lets the toolbar **Live**
pill pause/resume every sampling page — `MainWindowViewModel.ToggleLive` now routes through it over
`Nav.NavItems` (Dashboard + Network) instead of calling the Dashboard directly. Toolbar **Refresh**
routes through the existing `IRefreshablePage` (re-samples throughput, re-reads adapters/connections,
re-pings, re-resolves DNS). The ping/DNS console insets use a **fixed dark surface + fixed text
colours** (kept dark in both themes so the green/blue console text stays readable). **Deferred:**
IPv6 connections (the OWNER_PID tables use different 16-byte-address structs).

## Processes

**Live and functional** (built in phases). A Task-Manager-style live process view: the
list **split three ways — Apps / Background processes / Windows processes** (per `ProcessClassifier`
+ `ProcessCategory`), per-process **PID / status / CPU % / Memory / Disk / GPU %**, **sortable
column headers**, a summary strip (**process counts per group**, **total CPU %**, **total
Memory %**, **total thread count**), **End task** (behind a confirmation overlay — killing a
process is destructive) acting on the whole selection, and native **Properties** (the exe's shell
property sheet) acting on the primary row. Multi-process apps **collapse into a single entry** with aggregate metrics,
expandable via a chevron: `ProcessTreeBuilder` nests a
process under its parent only when the parent is in the snapshot **and shares the same image name**,
so Edge's ~27 `msedge.exe` helpers fold into one Edge row while unrelated apps aren't swallowed under
`explorer.exe`. Data is **in-box, no new dependencies, no admin**: `System.Diagnostics.Process`
(CPU % via `TotalProcessorTime` diff, memory, threads, status, exe path), a feature-local
`GetProcessIoCounters` P/Invoke for Disk MB/s, PDH `\GPU Engine(*)` grouped by the `pid_` token for
GPU %, and `ProcessClassifier`'s kernel32/user32/dwmapi P/Invoke for the two things managed
enumeration can't report: **parent PIDs** (a Toolhelp32 snapshot) and the **category** — the classic
"alt-tab window" test via `EnumWindows` marks an **App** (UWP frames re-attributed from
`ApplicationFrameHost.exe` to the hosted process), Session 0 isolation via `ProcessIdToSessionId`
marks a **Windows** process, and everything else is **Background**. Task Manager's own rules are
undocumented heuristics, so this is "close and correct", not byte-exact on every edge case.
**Live on Linux too (M9).** `LinuxProcessSnapshotProvider` walks `/proc` and reads five small files per
process — `stat`, `status`, `cmdline`, `cgroup`, `io` — of which **only `stat` is required**: a PID whose
`stat` has gone is a process that exited mid-walk and is skipped, which is the normal case rather than the
exceptional one. `ProcessGpuSampler`, `ProcessMemorySampler` and `ProcessClassifier` are reached only from
the Windows provider, so the Linux one replaces all three rather than seaming them. Names come from
`cmdline`'s first **NUL-separated** argument (basename, **no `.exe`** — that suffix is the Windows
provider's), falling back to `comm`, which truncates at 15 characters. CPU% is the `utime + stime` delta
over `USER_HZ` (hardcoded 100 — there is no rootless `sysconf(_SC_CLK_TCK)`), wall clock and core count.
`LinuxProcessClassifier` replaces the window test with `/proc/[pid]/cgroup`, because **the X11 route is a
dead end**: the target desktop is GNOME on Wayland, where no client may enumerate another's windows by
design. Its rules run in order — kernel thread → System; root-owned or `system.slice` → System;
a `.service` leaf → Background; `app.slice` or an `app-*.scope` leaf → App; else Background. **The
`.service` test must precede the `app.slice` test**, because modern systemd puts user *units* inside
`app.slice` alongside launched app scopes, and the other order files every user daemon as a foreground app.
The kernel-thread rule keys on an empty `cmdline`, so it **exempts zombies** — a zombie has lost its
address space too, but it is the corpse of a user process and its cgroup still places it.
`ProcessGroupNames` captions the third group **"System processes" on Linux**, since
`ProcessCategory.Windows` means "Windows process" on one platform and "system process" on the other; the
enum member keeps its name because only the display strings differ. **Permanent gap:** per-process GPU has
no rootless Linux source, so that column is always 0 — not a TODO.
**The per-process Network ("NET") column was REMOVED BY DESIGN** (2026-07, branch
`processesRemoveNET`) — there is no in-box, non-admin per-process network-rate API on Windows (Task
Manager uses ETW kernel providers, needing the `TraceEvent` package + admin), so rather than ship a
permanent "—" the column was deleted outright: header, data cell, sort key and all. This is **not
deferred work** — do not re-add the column or build toward it without an explicit task. The table is
7 columns.
**Reworked on branch `performanceTabUpdates` (2026-08)** — nine gaps closed in eight phases. What changed, and the decisions
behind it that must not be quietly undone:
- **Columns never drop; the table scrolls.** `ProcessTableLayout` lost its four drop-tier definition
  strings and its `ShowStatus`/`ShowDisk`/`ShowGpu` flags. The header and the rows now share one
  horizontal `ScrollViewer` whose content is floored by `MinTableWidth` (from the shared
  `WeightedRowLayout.RequiredWidth`, so one piece of arithmetic answers "does this weighted row still
  fit" for the widget board and this table alike). **The scrolled content's Width is pinned to the
  viewport on purpose:** a horizontal scroller measures its child with infinite width, where a
  `TextTrimming` cell reports its FULL text width — without the pin, one long process name widens the
  table past the window and leaves the scrollbar up for good. *Known cosmetic cost:* in a narrow window
  the row list's vertical scrollbar is inside the scrolled content, so it sits off screen until you
  scroll right (the wheel still works). Pinning it to the viewport means taking it out of the
  shared-width content, which is exactly what the header's 30px gutter exists to keep aligned.
- **Columns are drag-reorderable and persisted.** `ProcessColumnId` + `ProcessColumns` (one table of
  per-column minimum width and weight) + `ProcessColumnOrder` (the codec). Each cell binds its
  `Grid.Column` to a view-model index, so a reorder moves cells rather than rebuilding the table.
  **Name is pinned leftmost and is not draggable** — it owns the tree indent, the expand chevron and the
  selection box, and a hierarchy indented from the middle of the table is unreadable.
  **The header drag takes its pointer capture on the first move past the threshold, NOT on the press.**
  Header cells are buttons; capturing on the press strips the capture the button takes for its own
  click and every column stops sorting. `MoveColumn` is silent so the drag can preview live; only the
  release calls `CommitColumnOrder`, or one gesture rewrites `settings.json` dozens of times.
- **Multi-select.** A `HashSet<int>` of selected PIDs is authoritative — rows are transient, so like
  `_expandedPids` the set is what survives a poll, a re-sort and a filter. A box per row, Ctrl-click,
  Shift-range and a drag down the list all feed it, and a box on each group header takes the group.
  Ranges read straight down the screen, through the group headings, not per group. **Selection is
  pruned against the LIVE processes, not the visible rows:** a row the filter is hiding is still a
  process the user picked, and only exiting drops it. `SelectedRow` survives as the *primary* row, for
  the things that can only act on one (Properties' shell dialog).
- **The selection boxes are hand-drawn on `Button.bare`, not Fluent `CheckBox`es.** That template
  carries a fixed 20px box and its own minimum height, which between them added 16px to every row;
  constraining it clipped the box into a lozenge with no tick rather than scaling it. Do not "simplify"
  them back to a `CheckBox`. (`CheckBox.optionCheck` is still the right control in the options popup,
  where the row height does not matter.)
- **End task ends the whole selection**, carries on past a protected or already-exited process, and
  names a single failure while counting several. It works over the selected **PIDs**, not the visible
  rows, for the same reason the pruning does. The kill sits behind `IProcessTerminator` purely so it is
  testable — it used to be a bare `Process.Kill()` no test could reach without killing something on the
  machine running the suite.
- **The actions live on the table's filter row**, not the summary strip, beside the rows they act on and
  next to a selection count. Rows also carry a context menu (End task / Properties / Expand-collapse /
  Copy PID) **declared once and shared**, never per row — a per-row `MenuFlyout` would build a few
  thousand controls for a list this long. Nothing in it binds to a row: the right-click puts the row
  into the selection first, **on the tunneling press**, so it lands before the flyout opens.
  `ContextRequested` was tried for that and silently never fired.
- **Group sections fold**, and double-clicking a row does what its chevron does. Folding hides the list
  and nothing else — the count, the filter and the selection keep meaning the same thing.
- **Placeholder rows until the first enumeration lands**, because an empty list reads as "no processes
  running". `HasLoaded` is set on success AND on failure, never on cancellation: a page that failed has
  an answer, whereas a cancelled read means the user left and nothing was learned. The summary reads
  `Placeholders.NoReading` rather than `0` until then, and on failure, since the count is unknown, not
  zero. There is deliberately **no grace period** (unlike File Explorer's load strip): that exists to
  stop a spinner flashing on a fast transition, and this is the page's initial state.
- **What is remembered, and what is not.** Column order is persisted by default, with a Reset in the
  Options popup. Folding and sorting are **opt-in** (`RememberCollapsedGroups` / `RememberSort`), since
  both are usually a glance rather than a preference. Each reports a change only while its toggle is on,
  nothing is written for a toggle that is off, and seeding a saved value on startup is quiet so it does
  not write straight back. `PreferencesChanged` is the one event the shell hooks to `Persist`.
- **Row density** was tightened (`procRow` padding 16,5). `Button.chev`'s negative margin must stay in
  step with it, as its own comment says. `SortableColumnHeader` gained `ContentAlignment`: both call
  sites used to align the *control*, which shrank it to its label and left the rest of the column dead
  to a click.
Shared code this produced: `OrderResolver` (`WidgetOrders.Resolve`'s body, now reached by columns too),
`EnumListCodec`, and the promotion of `CheckBox.optionCheck` and `ToggleButton.optionsToggle` to
`SharedStyles` (their File Explorer copies deleted — a local style silently outranks the shared one).
Follows the shared page-lifecycle pattern (constructed once in the shell; `IRefreshablePage` +
`ILiveSamplingPage` + `IActivatablePage` + `IDisposable` + `ISelfScrollingPage`), the Network tab's keyed-diff live table
(via the shared `CollectionReconciler`, so rows are reused and the list doesn't flicker), and the
File Explorer sortable-header + Properties patterns. The list polls on its own 2 s timer
(enumerating every process is heavier than a single counter); the summary strip's system-wide
CPU %/Memory % come from the shared `SystemMetricsService`.

## Performance

**Live and functional** (built in phases). A Task-Manager-style resource
drill-down per the design comp: a left **resource-selector** rail (CPU · Memory · Disk 0 (C:) · GPU ·
Ethernet) of `ResourceRow` item VMs swaps a right **detail pane** — one large utilization chart
(reuses the shared `Sparkline`, fixed 0–100 axis + gradient fill + background grid) plus a 4-tile stat
strip (`StatTile` item VMs). The **rail scope** segments ("Primary" / "All devices", which collapse the
multi-instance categories to one row each) head the rail itself: over the whole page they read as if they
scoped the detail pane, which they never did. The **adapter row** shows both directions, "↓ receive" over
"↑ send", each in its own series tint — under the adapter's name a receive-only figure described half its
traffic. This is the one chart on which every grid line is labelled both ways, both axes are named, and the
per-core / per-engine cells carry their two ends; see the charting grades in AGENTS.md. Self-contained tab under `src/Tabs/Performance/` (`PerformanceView` +
`PerformanceViewModel`), master-detail like File Explorer via **`ISelfScrollingPage`**, reusing the
selectable-item pattern (`NavItem` / `FilterOption`) and shared styles. Series colours come from
`ChartPalette` through `ThemeService.BrushFor`, keyed by each row's `ChartSeries` identity — this tab
parsed its own hex literals until that changed, so CPU read one colour here and another on the
Dashboard. **All five resources are wired live**: each subscribes to the shared `SystemMetricsService`
(CPU / Memory / Storage / GPU / Network), keeps its own 60-sample `MetricHistory`, and pushes into the
selected row; static hardware labels load once via the
`*InfoProvider` async-WMI providers. Implements `IRefreshablePage` (toolbar Refresh re-samples every
metric), `ILiveSamplingPage` (Live/Pause is the shared service's), `IActivatablePage` (it samples only
while it is the visible tab) and `IDisposable`. No new packages,
no new shared controls. The CPU **Speed** tile is live: a page-local `IProcessorFrequencySampler`
(`src/Services/SystemMetrics`, chosen by `ForCurrentPlatform()`). The Windows arm reads the PDH
`\Processor Information(_Total)\% Processor Performance` ratio and `CpuSpeedFormatter` scales the WMI
base clock (`CpuStaticInfo.MaxClockMhz`) by it — exactly Task Manager's Speed figure, so it rises above
the base clock under Turbo (deliberately uncapped) and falls at idle. The Linux arm instead reports an
absolute MHz reading, which the formatter uses as-is; that is why `ProcessorClockSample` carries both
shapes rather than one number. Pumped on the page-local throughput timer (fixed 1 Hz, not retimed by the
Settings refresh interval) and on Refresh; degrades to "—" if no source is readable. This is
page-local, like the disk/GPU/per-core samplers — the shared CPU feed carries only the clamped
utilisation figure, and this reads a *different* counter, so `ProcessorUtilityCpuSampler` /
`SystemMetricsService` were untouched. The Memory **Cached** tile is live too:
`WindowsSystemPerformanceProvider` (in `Services/SystemMetrics`, behind `ISystemPerformanceProvider`;
it was page-local as `SystemCacheProvider` when first built) calls the in-box psapi
`GetPerformanceInfo` and scales its
`PERFORMANCE_INFORMATION.SystemCache` (pages) by the struct's own `PageSize`, which `MemoryCacheFormatter`
renders as binary GB — Task Manager's own "Cached" figure (verified against it: 15.3 GB on the tile vs
15.42 GB summed from the standby + modified page-list counters). **This corrects the old rationale**,
which claimed there was no source short of "adding a PDH counter to the pure-Win32 memory sampler": it
is *not* a PDH counter, needs no admin and no package, and — like `GlobalMemoryStatusEx` — is an
absolute one-shot reading, so it needs no rate-style sampler. (Do **not** reach for the PDH
`\Memory\Cache Bytes` counter instead: that reports the system *working set*, a different and far
smaller number — 0.5 GB where Cached was 15 GB.) Deliberately a **page-local** P/Invoke under
`src/Tabs/Performance/` (the `ShellInterop` / `ConnectionsInterop` precedent), so the shared
`MemoryUsageSampler` and the `MemorySample` record — consumed by Dashboard and Processes too — were
**left untouched**. (Both have since moved and been seamed: the reader is
`WindowsSystemPerformanceProvider` behind `ISystemPerformanceProvider` in `Services/SystemMetrics`, and
the sampler is `WindowsMemoryUsageSampler` behind `IMemoryUsageSampler` — M6. The rationale above still
holds; only the names and the folder changed.) Unlike the Speed tile it is read inside `UpdateMemory` on the shared memory tick,
so it re-times with the Settings refresh interval, pauses with the Live pill, updates on Refresh, and
blanks to "—" alongside its neighbours if that feed faults. The GPU **VRAM** tile is live too, and it is
the one tile that is **not** sampled: DXGI's dedicated video memory is static per adapter, so
`GpuAdapterProvider`'s `DedicatedVideoMemory` (already read for the multi-GPU work, previously discarded)
is now carried on `DeviceInstance.VramBytes` and set once in `BuildGpuRows` when the row is built —
re-read only when Refresh re-runs the inventory. It is formatted by **reusing** `FileSizeFormatter`
(File Explorer's binary byte humanizer, already called cross-tab by Storage), so a 12 GB discrete card
and a 512 MB integrated adapter each read naturally instead of forcing a fixed GB unit; zero/absent
yields "—". `DeviceInstance` gained a trailing optional `ulong? VramBytes` and `DeviceInventory.Compose`
passes it through (both under explicit sign-off; `GpuAdapterProvider` itself was untouched).

**GPU Temp and Power are live too** (2026-07). This **supersedes the old claim that
there was no source** — there is none *in-box*, but every display driver installs its vendor's own SDK, so
no package, no redistributable and **no admin** are needed. Support by vendor:
- **NVIDIA — temperature and power.** Temperature from NVAPI's `NvAPI_GPU_GetThermalSettings`, power from
  NVML's `nvmlDeviceGetPowerUsage`. Power deliberately does **not** go through NVAPI: its power call
  (`ClientPowerTopologyGetStatus`, `0xEDCF624E`) is **absent from NVIDIA's published `nvapi_interface.h`**
  and known only from reverse-engineered sources, whereas the NVML one is documented and supported.
  Verified on a GeForce RTX 3060 against `nvidia-smi` — power agreeing to the watt over consecutive samples.
- **AMD — temperature (verified), power (written but unverified).** Temperature from ADL's PMLOG snapshot,
  preferring `TEMPERATURE_EDGE` and falling back to `TEMPERATURE_GFX` (integrated parts report no edge
  sensor) — verified on a Radeon(TM) Graphics iGPU. Power reads `BOARD_POWER` on **discrete boards only**
  and has never produced a reading on real hardware; see *Deferred work* before trusting it. **ADL, not
  ADLX**: ADL's exports are flat C functions needing only `[DllImport]`, where ADLX's C API is
  interface/vtable-based.
- **Intel — neither** (no reader written; see *Deferred work*).

Design notes worth keeping: each vendor is one `IGpuSensorReader` behind `GpuSensorProvider`, so vendors
are swappable and never entangled; readers are contracted **never to throw**, and one that does is logged
once and dropped for the session. Every vendor **and every metric** degrades independently — a board that
reports temperature but not power shows one and blanks the other. Attribution is by **PCI identity, not
LUID** (no vendor SDK exposes a LUID): `GpuAdapter`/`DeviceInstance` now carry a `GpuPciId` read from the
`DXGI_ADAPTER_DESC1` fields that were already being fetched and discarded, and NVAPI/NVML/ADL all report the
same ids — so the join is exact, not positional. Each vendor's libraries initialize **lazily** on the first
adapter that reader is asked about (measured ~37 ms for NVIDIA, ~26 ms for AMD, once each), and cost
~0.6–1.0 ms per 1 Hz tick for both GPUs thereafter. Two ADL traps are handled and must not be "fixed": its
`AdapterInfo.iVendorID` is **unusable** (reports `0x03EA` for AMD, `0x000A` for NVIDIA), and it enumerates
other vendors' adapters, listing each GPU once per display output — hence `PnpPciParser`. This reached
outside `src/Tabs/Performance/` only to add `GpuPciId` to `GpuAdapterProvider` and thread it through
`DeviceInventory`, both under explicit sign-off.

## Toolkit

Designed as a **"Commands"** tab and shipped as **Toolkit** (nav label, folder, namespace and type
names). "Commands" survives only in older discussion; nothing in the repo uses it, so **always write
Toolkit**. It is the **ninth** tab, sitting between Hardware and Settings, which is why the
Ctrl+digit tab jumps run **Ctrl+1 … Ctrl+9**.
- **What is built:** a filter bar (search box + category chips + result count) over a grouped command
  list, beside a pinned 340px **Execution Log** panel. The taxonomy is treated as *format*, not data:
  four categories (`ToolkitCategory` — Folders / System Tools / Diagnostics / Docs & Links) and five
  entry kinds (`ToolkitEntryKind`, each with a badge label, colour and glyph) exist and are tested.
- **Execution is live.** Clicking a row runs its `ToolkitAction` through `ToolkitRunner` and prepends a
  stanza to the Execution Log. A row that names a place on disk is the exception: it opens the in-app File
  Explorer instead (`ToolkitViewModel.OpenInApp` → `FileExplorerRevealRequested` → the shell's existing
  `RevealFile`), with Windows Explorer on the row's other icon.
- **The safety invariants.** The built-in table used to be an allow-list, and the Toolkit had no
  free-form entry at all. That changed when users gained the ability to author their own commands
  (`ToolkitCommand` + the "+ Add command" form). What was actually load-bearing is kept, and **these four
  are the rules — do not weaken them**:
  1. **Arguments always reach the OS as a list.** `ToolkitAction.Arguments` is an `IReadOnlyList<string>`
     passed to `ProcessStartInfo.ArgumentList`; nothing is ever joined into a command line, and `Capture`
     keeps `UseShellExecute = false`. There is no `cmd /c` anywhere. `ToolkitArgumentParser` splits the
     user's typed string, so `&`, `|`, `>` and `$(…)` reach the program as literal text — no shell exists
     to interpret them. `ToolkitAction.CommandLine` is display-only and nothing is ever run from it.
  2. **Elevation is catalog-only.** `ToolkitCommandType` (the form's types: `FolderPath`, `Launch`,
     `Capture`, `Url`) has **no elevated member**, so no form input can produce a row that raises UAC.
     `sfc /scannow` remains the one elevated entry on Windows, and the Linux table has none at all.
     Elevation is its own `ToolkitActionKind` (not a flag) because Windows refuses to redirect a `runas`
     process's streams, so "elevated *and* captured" is not expressible. Both halves are asserted per
     catalog, and `ToolkitCatalogInvariants` asks every table that only a console command may elevate.
  3. **`OpenUrl` is https-only**, refused in `ToolkitRunner.RunAsync` regardless of where the action was
     authored — which covers user URLs for free. `ToolkitCommandValidator` says the same thing earlier, in
     the form, where it is still fixable.
  4. **Nothing persisted ever runs on its own.** A stored command becomes a row; a row runs only when it is
     clicked. The Execution Log's `$` line prints the resolved target and arguments, so a row whose label
     disagrees with what it runs is visible the moment it fires.

  Accepted, documented residual risk: `settings.json` can now put a mislabelled runnable row on the page if
  it is tampered with. That is not an escalation — anything able to write `%AppData%\DashDetective` can
  already write `shell:startup` — and it cannot fire without a click.
- **Catalog:** four authored categories — Folders, System Tools, Diagnostics (parameterised ping/tracert
  and the elevated `sfc /scannow`) and Docs & Links — plus **My Commands** (`ToolkitCategory.Custom`),
  which holds what the user authored. Adding a *built-in* row means editing that platform's table and
  nothing else. What everything downstream reads is `ToolkitViewModel.AllEntries` (catalog + custom),
  **not** the catalog — filter, grouping, pins and the search provider all go through it.
- **The command set is per-platform; the copy is not.** `IToolkitCatalog.ForCurrentPlatform()` resolves
  `WindowsToolkitCatalog`, `LinuxToolkitCatalog` or the empty `UnsupportedToolkitCatalog`; `ToolkitCatalog`
  kept `Categories`/`HeaderFor`/`LabelFor`, which read identically everywhere and are reached statically
  by `ToolkitEntry.BadgeLabel`, `ToolkitGroup` and `ToolkitFilter`. `ToolkitViewModel` takes the catalog
  through an internal ctor (`FileExplorerViewModel`'s shape); the shell still builds it with `new()`.
  **The catalogs are singletons on purpose** — `IsPinned` is live state on the rows, so a fresh list per
  call would give each reader its own unpinned copy.
- **The Linux table is not a translation of the Windows one.** Folders are `~`, `~/.config`,
  `~/.local/share`, `~/.config/autostart`, `/etc`, `/var/log`, `/tmp`; tools are GNOME's; diagnostics are
  coreutils, iproute2 and systemd. Three decisions worth not re-litigating: **no row elevates** (`Elevated`
  means the `runas` verb, and `pkexec` is a later milestone's), **`dmesg` is deliberately absent** because
  Ubuntu and Debian set `kernel.dmesg_restrict` and a non-root run only ever prints a permission error
  (`journalctl -k` is the row that works), and **`ping` carries `-c 4`** because Linux ping runs until
  interrupted and would otherwise end at the timeout on every run. A program that is not installed is a
  **run-time** answer — filtering the table at startup would mean shelling out once per row before the
  page could draw.
- **Catalog rules are asserted against every table at once.** `ToolkitCatalogInvariants` is an abstract
  class with one subclass per catalog, so a rule added there is asked of all of them **and runs on both CI
  legs** — the tables are string literals, so nothing about them needs a Linux host to check. Only what is
  genuinely one platform's stays in its own class (each table's elevated row pinned by name, the
  "administrator" wording, `dmesg`'s absence). A catalog test that branched on `OperatingSystem.IsLinux()`
  would leave the other table unchecked wherever it ran; there is exactly one such branch in the feature,
  in `ToolkitCatalogSeamTests`, and it only maps host → catalog type.
- **Elevation is one authored row per table** (`sfc /scannow`, `fwupdmgr refresh`) and reaches the OS
  through a different mechanism on each: the `runas` verb on Windows, `pkexec` as the launched program on
  Linux. **A declined prompt is worded on Windows and silent on Linux, deliberately.** `runas` fails
  synchronously inside `Process.Start`, so `ToolkitRunner` can catch `ERROR_CANCELLED`; `pkexec` reports
  refusal as exit 126 only after the launch returns. Waiting for it would hold `sfc /scannow`'s log entry
  open for the many minutes it runs, and 126 is indistinguishable from the program's own exit code — so
  the launcher does not wait, and the Linux side loses only the wording.
- **User-authored commands.** `ToolkitCommand` is what the user typed; `ToolkitCommandFactory` turns it
  into an ordinary `ToolkitEntry` through the catalog's own `ToolkitAction` factories, so `ToolkitRunner`
  cannot tell a user's row from an authored one and there is no second execution path to keep safe. The
  *typed* payload is what persists (via `ToolkitCommandCodec` into `AppSettings.CustomCommands`, `ToolkitPins`'
  encoding one level deeper: `0x1E` between records, `0x1F` between fields, enums by name) — so the edit
  form re-fills with the user's own words rather than something reconstructed from an action. Commands load
  **before** pins in `ApplySettings`, or a pin naming one finds nothing.
- **A custom command can appear twice.** It is always in My Commands, and additionally in the category the
  user labelled it with (`ToolkitEntry.SecondaryCategory`) — the one case where a command deliberately owns
  two rows. `ToolkitFilter.Matches` therefore accepts *either* of its categories, and picking a chip
  collapses it back to the one section asked for. Two consequences: `RebuildGroups` counts **distinct rows**
  for the count label, and `ToolkitView.FindRows` flashes **every** row carrying a revealed command rather
  than the first. Pinning still lifts rather than copies, so a pinned labelled row leaves both sections.
- The Execution Log **exports** to a text file (`BuildLogText` + a save picker in the view code-behind,
  the `SettingsView.SaveAsync` flow). Stanzas keep the order they are shown in — newest first — so the
  file reads as what was on screen.
- **Pinned favourites** persist through `AppSettings.PinnedCommands`, encoded by `ToolkitPins` as one
  opaque string (the `RecentSearches` pattern, ASCII record separator). Pins are stored **by command
  text, not by index**, so a list that gains or loses a row between sessions cannot silently re-point
  them; a pin naming a command that no longer exists is dropped when applied. Storing by live text is
  also why renaming a custom command keeps its pin without any identity field: `EncodePins` reads current
  state, so the next persist writes the new title. A pinned row is **lifted** into the Pinned section, not
  copied there — it is the one row the user asked to be able to find in a fixed place. The chip and search
  term still apply to pinned rows. Note `IsPinned` lives on the catalog's shared entries (there is
  exactly one Toolkit page, so they *are* its rows, and that is why each catalog is a singleton) — tests
  that touch pins must reset them rather than assume a clean slate.
- **Docs & Links rows are labelled by title, not URL** (a Learn URL ellipsizes to nothing in the row's
  mono label); the URL still reaches the Execution Log through `ToolkitAction.CommandLine`, so what was
  opened is on the record. Every URL was **fetched and confirmed live** when authored — one candidate
  (`troubleshoot/.../use-system-file-checker-tool`) was a 404 and was replaced by the
  `windows-commands/sfc` reference. A test pins that every link is `https://`, since the runner refuses
  anything else and a non-https row could only ever be a dead button.
- **`sfc /scannow` is the only row that elevates**, and `ToolkitCatalogTests` pins that set **by name** —
  adding another must be a deliberate edit to that test, not something that slips in. It is `Elevated`
  rather than `Capture` for two independent reasons: Windows will not redirect a `runas` process's
  streams, and sfc runs for many minutes, which a captured command's timeout would cut short. The row
  carries an amber shield (fixed colour, like the kind badges) **and** says "needs administrator" in its
  description, because the shield is invisible to anyone reaching the row through universal search.
- **`ping`/`tracert` carry the only user input in the app**, and `ToolkitHostValidator` is the only place
  it is checked. Injection is already impossible (the value becomes one `ArgumentList` element), so the
  validator's real job is that **an accepted value cannot be a flag** — a DNS label may not begin with a
  hyphen, so `-t` and friends are refused. The box is seeded with the primary adapter's gateway via
  `ToolkitDefaults`, off the UI thread and **never over a value already typed**. The lookup itself now
  lives in `NetworkGateway` (`src/Services/Network`), shared with the Network tab's ping panel; what stays
  in `ToolkitDefaults` is only this table's answer to "no gateway", which is the `8.8.8.8` literal — a
  `ping <host>` row with an empty box would be a dead button. The Network tab answers it the other way. The log's `$` line shows `ToolkitAction.CommandLine` — the resolved target plus
  arguments — not the row's label, so a placeholder (`ping <host>`) and any flags the label omits
  (`tracert -h 20`) are both visible in the transcript.
- **The page has no separate busy flag, on purpose.** Refusing concurrent runs makes the generated
  command report `CanExecute` false while one is in flight, which disables every row's button by itself.
  The stanza is written to the log *before* the command runs and replaced **in place** on completion
  (reference equality, so a log cleared mid-run drops the result rather than resurrecting it), and it
  keeps the time the command **started** — stamping it on completion would put a 90 s `systeminfo` a
  minute and a half away from the click that caused it.
- System Tools rows are launched by their **bare command** (`services.msc`, `ncpa.cpl`, `regedit`), not a
  resolved path: `%windir%` and `%windir%\System32` are both on PATH, so ShellExecute finds them exactly
  as typing them into Run does. `.msc` opens through `mmc.exe`; `.cpl` has no explicit default verb, so
  ShellExecute takes the first — `cplopen` → `control.exe` — which is why the launch must **not** set a
  Verb unless it is deliberately elevating.
- The per-row **copy button is live**: it copies `ToolkitViewModel.CopyTextFor` — the same resolved
  command line the log would record — so a paste into a terminal does what clicking the row does, and a
  documentation row yields its URL rather than its title. A refused or half-filled host box is left off
  altogether rather than pasted as a dangling argument. It lives in the view code-behind because the
  clipboard is reached through the window's `TopLevel`, as `SettingsView.OnCopyDiagnosticsClick` is;
  `SetTextAsync` needs `using Avalonia.Input.Platform`. Success flashes the glyph accent for a second
  (the click needs an answer, and the log is for what *ran*).
- Self-scrolling (`ISelfScrollingPage`) so the log panel stays pinned while the list scrolls; the
  comp's `position:sticky` has no Avalonia equivalent. Wired to `IShortcutTarget` (`ShortcutScope.Toolkit`,
  `/` focuses the filter, `Esc` clears it) and to universal search via `ToolkitSearchProvider` — see the
  reveal gotcha in the *Universal search* write-up in the Appendix. Not `IRefreshablePage` /
  `ILiveSamplingPage`: the page has nothing live to sample or re-read.
- **Row hover carries no brush transition** — instant, like File Explorer's `fileRow` and Performance's
  `resCard`. A transition on the hover-bearing element animates the hover itself, so scrolling the list
  under a stationary pointer smears the highlight across every row it passes. The search-reveal fade
  therefore lives on its own `revealFlash` layer behind the row content, which owns the transition and
  has no `:pointerover` rule — the same split `SettingsView.settingRow` gets for free by having no hover
  state at all. That is also why the row's inset is a `Margin` on the inner grid rather than `Padding` on
  the row border: the tint has to span the whole row. **Do not merge the two back together.**
- Two layout decisions were made **against** the comp, both after seeing them fail on screen at the
  window's 920px minimum, and both should be left alone: the **filter bar wraps** (box + five chips
  overflow the content area, and the last chip was unreachable), and the **kind badge sits in its own
  row column** rather than inline after the command (two `Auto` columns do not shrink, so a long
  command clipped at the card edge and shouldered the badge out of view).

## Keyboard shortcuts

- **Keyboard shortcuts** — **fully live**, built in phases. A data-driven shortcut layer:
  `src/Shared/Shortcuts` holds the model (`ShortcutCatalog`, `ShortcutId`, `ShortcutScope`, `Shortcut`,
  `ShortcutGroup`, `IShortcutTarget`) and `src/Shell/Shortcuts` the listener (`ShellShortcutHandler`,
  `KeyboardFocus`). **`ShortcutCatalog` is the shipped default table, and `ShortcutBindings` is what is
  actually in force** — the defaults with the user's rebinds applied. One `ShortcutBindings` instance is
  shared by the key handler, Help, universal search and the Settings page, so all four describe and act
  on the same thing and cannot drift. Resolution and Help grouping live on the instance; the catalog
  keeps the pure table-taking helpers, so there is no second copy of either. Dispatch is
  a priority chain on `MainWindowViewModel.HandleShortcut` (open modal → current page via
  `IShortcutTarget` → global), and a handler reports whether it acted so an inapplicable key falls
  through. Resolution is **scope-aware**: a gesture may mean different things on different tabs
  (`Alt+↑` sorts on Processes, climbs a folder on File Explorer). **`Esc` has exactly one owner** — the
  chain, not individual controls. See *Keyboard shortcuts* in `docs/ARCHITECTURE.md`.
- **One `ShortcutId` may be bound in several scopes.** `/` → `FocusFilter` serves both the Processes
  filter and the Toolkit filter: the shell offers a resolved id to whichever page is current, so a new
  tab reuses the existing action rather than minting a near-duplicate id per tab. The catalog invariant
  is therefore "neither an action **nor** a gesture bound twice **within one scope**", which is what
  `ShortcutCatalogTests` pins.
- **Every shortcut is rebindable**, from the Keyboard card on the Settings page. A row arms a
  `ShortcutCaptureBox`, which swallows the next press and offers it to the view model; the rebind
  **replaces all of a shortcut's default gestures**, so rebinding Refresh leaves neither F5 nor Ctrl+R
  firing it. Rebinds persist as one opaque string (`ShortcutOverrideCodec`, ids and keys **by name** so
  reordering an enum cannot silently rebind a keyboard), and a per-row reset and a card-level
  "Restore default shortcuts" put them back.
- **The shell must stand down while a capture is armed.** Its listener is a tunnelling handler on the
  window, so it sees the press *before* the capture box does — without this, arming a box and pressing
  Ctrl+1 would navigate away instead of capturing. `SettingsViewModel.IsCapturingShortcut` is what
  `HandleShortcut` checks first, returning false so the key continues down to the box.
- **A clash is refused, not silently accepted**, and only **within one scope**. Cross-scope duplicates
  stay legal because they already are (`Alt+↑` on Processes and File Explorer), since only one tab is
  ever current. The capture box reports the conflict inline, naming the action that already holds the
  gesture.
- **The digit jumps run `Ctrl+1 … Ctrl+9`** (nine tabs). `ShortcutId.NavigateTab1..9` must stay
  contiguous — `MainWindowViewModel.HandleGlobal` maps them to nav positions by subtracting
  `NavigateTab1`, and its range guard names the last one, so a tenth tab means touching the enum, the
  catalog, that guard, and the Help copy in both `ShortcutCatalog` and `HelpContent`.
- Three features were built alongside it because the requested shortcuts had nothing to bind to: the
  **Processes filter box** (name/PID, with `ProcessFilter` as a testable static), **File Explorer
  back/forward history** (`NavigationHistory` plus back/forward/up buttons by the breadcrumb), and the
  **File Explorer editable address bar** (`Ctrl+L`, breadcrumb ⇄ path box).
- Also added: the shared **`TextBox.flat`** style (Fluent's focus state was painting a solid block and
  an accent underline over the search/filter/path field chrome).
- Deliberately **not** bound: `Space` for the Live pill (it activates whatever button has focus),
  `PageUp`/`PageDown` for Network paging (they stay as the connections list's scroll gesture), and
  `Tab` — accepting a ghosted completion is owned by the field showing one (`GhostCompletionBox`),
  because only the focused control knows whether there is a suggestion to accept, and Tab must go on
  moving focus everywhere else.
- **Superseded by universal search:** `Ctrl+F` is now the global search gesture on every tab, and `/`
  is the tab-local one — it focuses the Processes filter and the File Explorer address bar. Both `/`
  bindings are `AllowInTextInput: false`, which also fixed a bug where the key was consumed before
  reaching the box it had just focused, so a `/` could never be typed into it.

## Storage

- **Storage** — **fully live**, built in phases. A read-only drives/health view per
  the design comp: a top row of **drive summary cards** (name + health pill, model, usage bar, used/free,
  and live **Read / Write / Temp**) over a bottom row of a **Partitions** table (Vol · Label · File System
  · Capacity · Free) and a **Disk Activity (C:)** card (amber area chart + Active time / Avg response /
  Queue). Self-contained tab under `src/Tabs/Storage/` (`StorageView` + `StorageViewModel`), **page-
  scrolling like Network** (not `ISelfScrollingPage`), reusing `Border Classes="panel"`, the shared
  `Sparkline` (on the `ChartStorage` series key), and the built-in `ProgressBar` for the usage bars.
  Live sources: the drive cards from `PhysicalDiskProvider` + `StorageComposer` + `VolumeProvider`; the
  Disk Activity chart + Active time / Avg response / **Queue** readouts from the same page-local
  `IPhysicalDiskThroughputSampler`; per-disk **Read/Write** from the page-local
  `IPhysicalDiskThroughputSampler` (its own 1 Hz timer, deliberately not retimed by Settings); and each NVMe
  card's **Temp** from `DiskTemperatureProvider` (non-admin `IOCTL_STORAGE_QUERY_PROPERTY` health-log read,
  refreshed on a slow ~15 s sub-cadence of the throughput timer). Wired to `IRefreshablePage` /
  `ILiveSamplingPage` / `IActivatablePage`. Non-NVMe drives show "—" for Temp; SATA/HDD/USB drive temperature stays deferred
  (needs admin or vendor SDKs). No new packages, no new shared controls. (**GPU** temperature is no longer
  deferred — it is live on the Performance tab via per-vendor SDKs; see the write-up in the Appendix.)

## Page lifecycle

*Shipped — branch `backgroundBehavior`, 2026-08.*

**Pages no longer run while nobody is looking at them, and the app no longer touches the network unasked.**
Three rules, and they are load-bearing — a page that breaks one is a regression, not a style choice:

1. **A page's timers are built STOPPED.** `SamplingGate` (live ∧ on-screen) starts them; the shell moves
   activation with navigation and with hide-to-tray. A constructor that calls `Start()` puts the page back
   to sampling from launch on a tab nobody opened.
2. **A deactivated page DROPS its `SystemMetricsService` subscriptions** (`MetricSubscriptions`). The feeds
   are ref-counted, so a page that stays subscribed and merely ignores its callbacks still pays for them.
   The service's own alert watcher follows the same rule, behind `AlertsEnabled`.
3. **Nothing reaches the network on its own.** The Network tab's ping is opt-in and defaults to the
   machine's own gateway; its DNS lookup runs only when asked. Neither may move back into a constructor.

What stays in constructors is the **one-shot** loads, because the shell's exported report and universal
search read them from pages the user may never open (`LoadAdaptersAsync`, the Processes list load, the
hardware/inventory loads). Measured on the development machine: ~3 % of one core on a visible sampling tab
→ ~0.2 % once another tab is selected, and 4.7 % → ~0.8 % hidden in the tray. The residue is the shell's
own 1 Hz clock timer, which is not gated.

Closing the window also **says so once** — see `/Shell/TrayNotice` in *Folder Structure*.

## Widget system

*Shipped — branch `widgitUpdates`, 2026-08.*

**A widget is a `WidgetPanel`, and a page's widgets are children of one `WidgetBoard`.** Nineteen
widgets across seven tabs were hand-rolled `Border Classes="panel"` + `StackPanel` + `panelTitle`; that
shape now exists once. Five rules, all load-bearing:

1. **A titled panel is a `WidgetPanel`.** `Title`, `Subtitle`, `HeaderLead` (content against the title),
   `HeaderContent` (content at the far end), `WidgetId` as `{page}.{slug}`. A surviving
   `Border Classes="panel"` is a *surface* — a pane, the Help modal, a drive card — not a widget.
2. **A page's widgets go in one `WidgetBoard`, never fixed rows.** The board packs rows to fit and caps
   each widget with `WidgetBoard.MaxSlotWidth`, so a wide window buys another column rather than a wider
   widget. `MaxSlotWidth` is attached to the board, not the child's own `MaxWidth`, because Avalonia
   clamps a stretched child and then *centres* it.
3. **Order is dragged by the header and persisted by widget id**, never by index. `WidgetOrders.Resolve`
   drops ids a page no longer has and keeps a newly added widget at its declared position.
4. **The board must never touch `Panel.Children`.** Reorder is an arrange-time permutation over its own
   index list; mutating `Children` mid-layout is re-entrant and detaches a live `Sparkline` from its feed.
5. **A tunneling input handler must only undo what it started.** `WidgetBoard.OnPointerUp` once released
   the pointer capture on *any* release, which stripped the capture a button took on its own press —
   every button on Dashboard, Network and Storage was dead for three phases, with build, tests and
   screenshots all clean. Guard on the press you actually claimed.

Also shipped on this branch: `WidgetTable` (header above a scrolling body, one gutter for both — Network
connections and Storage partitions only; File Explorer measures column drops off its own header width and
was deliberately left alone, and **Processes no longer drops columns at all** — see its own section below),
the collapsing toolbar search, the Ping console filling its widget and keeping as much scrollback as fits,
and `Dimensions.axaml`.

**Deferred on this branch:** differentiating the tab header from the universal toolbar header. The user
is doing design work first — do not start it without a task.

## Multi-GPU

*Shipped — branch `multiplePerformanceCards`, 2026-07.*

No longer deferred. Multiple physical GPUs now render one card per adapter on the Dashboard and one rail
row per adapter on the Performance tab (each with its own overall % + per-engine Detailed grid), following
the disk multi-instance pattern. Key pieces (the DXGI research below was correct and is now implemented):
- `GpuAdapterProvider` — DXGI (`dxgi.dll`, `CreateDXGIFactory1` → `EnumAdapters1` → `GetDesc1`) is the
  authoritative LUID→name map and flags software adapters. Called via **raw vtable function pointers, not
  `[ComImport]`** (built-in COM is disabled by a runtime switch: `NotSupportedException: Built-in COM has
  been disabled`); no `unsafe`, no csproj change. Also exposes true VRAM (WMI `AdapterRAM` is 4 GB-capped),
  which now feeds the Performance tab's GPU **VRAM** tile — carried per adapter on
  `DeviceInstance.VramBytes` (see the Performance write-up in the Appendix). No longer deferred.
- Per-GPU utilisation is **attributed by adapter LUID**: the PDH `\GPU Engine(*)` instances are keyed by
  `luid_0x{High:x8}_0x{Low:x8}`; `GpuUsageSampler.SampleAdapters()` groups by that token.
- The card set is **DXGI non-software adapters ∩ the LUIDs present in the PDH engine counters**
  (`DeviceInventory.Compose`). The intersection is required — DXGI can list one physical GPU under several
  LUIDs, and also enumerates a software "Microsoft Basic Render Driver"; both are discarded.
- `Win32_VideoController` is still read by `HardwareInfoProvider` for the Hardware tab's spec card; the
  inventory uses `GpuAdapterProvider`. (The old single-name `GpuInfoProvider` was deleted once nothing
  called it.)

## Repo-hygiene / portfolio pass

> The two entries below are one-off passes outside the usual per-feature boundaries, each authorised
> under explicit sign-off. Recorded for history — completing them did **not** widen the working
> boundaries; any further cross-cutting change still needs its own sign-off.

*Completed 2026-07-18.* A portfolio `README.md`, a reader-facing
`docs/ARCHITECTURE.md` (distilled from this appendix), project metadata in the csproj (`Version 0.1.0`,
title/authors/copyright, retarget to `net10.0-windows` — since REVERSED to a neutral `net10.0` by the
cross-platform port), analyzer + warning gates (`AnalysisLevel latest`, `TreatWarningsAsErrors`,
`EnforceCodeStyleInBuild` — since moved to a root `Directory.Build.props` so both projects share them)
with a root `.editorconfig` encoding the
existing style, a `dotnet format --verify-no-changes` step in CI, and the Settings footer wired to a
real assembly version via `AppInfo` (`src/Shared`) instead of the old fictional string. This did
**not** change any feature behaviour (the footer text is the sole exception).

## De-duplication / composition refactor

*Completed 2026-07-19.* A cross-cutting pass over
`src/Shared`, `src/Services`, `src/Shell` and the Dashboard / Performance / Network / Processes tabs,
with **zero user-visible behaviour change**. It replaced the ~10× copy-pasted per-metric
`DispatcherTimer` + rolling-buffer pattern with `MetricChannel` + a shared `SystemMetricsService` (one
sampler set, ref-counted subscriptions, removing the duplicate PDH GPU/disk queries); consolidated the
chart/format/diff duplication into `MetricHistory`, `SparklinePoints`, `ChartScale`,
`HardwareNameFormatter` and
`CollectionReconciler`; added real shutdown disposal via a manual composition root in `App`; switched
`NavigationView`/`MainWindow` fan-out to `[NotifyPropertyChangedFor]`; replaced the reflection
