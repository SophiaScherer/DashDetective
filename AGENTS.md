# AGENTS.md — DashDetective

> **This is a living document.** It will be updated as features are added, removed, or reworked.
> Always read this file in full before making any changes. If instructions here conflict with
> something you infer from the codebase, this file wins.

## Project Overview

DashDetective is a system info console built with **Avalonia UI (C#)**. It is being developed
incrementally, one feature at a time, in a modular style. Each main feature lives in its own
folder and is developed largely in isolation from the others.

The planned top-level features are:

- Dashboard
- File Explorer
- Processes
- Performance
- Network
- Storage
- Hardware
- Toolkit
- Settings

Not all of these exist yet. Only build what is listed below as "currently active."

## Current Scope — READ THIS FIRST

**No feature is mid-build right now — every planned top-level feature is live.** Pick up only what a new
task explicitly assigns, and do not modify a live feature without an explicit scope expansion.

**Already-live features — read for consistency (shared styles, naming, the page-lifecycle /
self-scrolling patterns)** (full write-ups in *Appendix — Completed Feature Details*): the shell **Navigation bar**,
**Dashboard**, **Settings** (fully live — Appearance, Navigation, Monitoring and Export & Data),
**File Explorer**, **Network**, **Processes**, **Performance**, **Hardware**, **Storage** (live —
drives/health view; status below), **Toolkit** (live; status below) and **Keyboard shortcuts**
(status below). Two cross-cutting passes are also complete (repo-hygiene / portfolio pass;
de-duplication / composition refactor) — write-ups in the Appendix.

**Toolkit — implementation status** (LIVE):

- **Toolkit** — the design document's **"Commands"** tab, shipped in the live app as **Toolkit** (nav
  label, folder, namespace and type names; "Commands" is a design-doc-only name). The UI was built in
  phases (plan: `C:\Users\User\.claude\plans\create-the-ui-for-sharded-minsky.md`); execution and the
  command set are being built in phases now (plan:
  `C:\Users\User\.claude\plans\develop-a-phased-plan-sunny-crystal.md`). It is the **ninth** tab,
  sitting between Hardware and Settings, which is why the Ctrl+digit tab jumps now run **Ctrl+1 …
  Ctrl+9**.
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

**Keyboard shortcuts — implementation status** (LIVE):

- **Keyboard shortcuts** — **fully live**, built in phases (plan:
  `C:\Users\User\.claude\plans\create-a-phased-plan-crispy-island.md`). A data-driven shortcut layer:
  `src/Shared/Shortcuts` holds the model (`ShortcutCatalog`, `ShortcutId`, `ShortcutScope`, `Shortcut`,
  `ShortcutGroup`, `IShortcutTarget`) and `src/Shell/Shortcuts` the listener (`ShellShortcutHandler`,
  `KeyboardFocus`). **`ShortcutCatalog` is the single source of truth** — the key handler resolves
  against it and the Help modal renders from it, so bindings and documentation cannot drift. Dispatch is
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

**Storage — implementation status** (LIVE):

- **Storage** — **fully live**, built in phases (plan:
  `C:\Users\User\.claude\plans\develop-a-plan-to-radiant-raven.md`). A read-only drives/health view per
  the design comp: a top row of **drive summary cards** (name + health pill, model, usage bar, used/free,
  and live **Read / Write / Temp**) over a bottom row of a **Partitions** table (Vol · Label · File System
  · Capacity · Free) and a **Disk Activity (C:)** card (amber area chart + Active time / Avg response /
  Queue). Self-contained tab under `src/Tabs/Storage/` (`StorageView` + `StorageViewModel`), **page-
  scrolling like Network** (not `ISelfScrollingPage`), reusing `Border Classes="panel"`, the shared
  `Sparkline` (on the `ChartStorage` series key), and the built-in `ProgressBar` for the usage bars.
  Live sources: the drive cards from `PhysicalDiskProvider` + `StorageComposer` + `VolumeProvider`; the
  Disk Activity chart + Active time / Avg response / **Queue** readouts from the shared `StorageUsageSampler`
  feed (via `SystemMetricsService`); per-disk **Read/Write** from the page-local
  `IPhysicalDiskThroughputSampler` (its own 1 Hz timer, deliberately not retimed by Settings); and each NVMe
  card's **Temp** from `DiskTemperatureProvider` (non-admin `IOCTL_STORAGE_QUERY_PROPERTY` health-log read,
  refreshed on a slow ~15 s sub-cadence of the throughput timer). Wired to `IRefreshablePage` /
  `ILiveSamplingPage` / `IActivatablePage`. Non-NVMe drives show "—" for Temp; SATA/HDD/USB drive temperature stays deferred
  (needs admin or vendor SDKs). No new packages, no new shared controls. (**GPU** temperature is no longer
  deferred — it is live on the Performance tab via per-vendor SDKs; see the write-up in the Appendix.)

### Page lifecycle — SHIPPED (branch `backgroundBehavior`, 2026-08)

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

**Nothing is out of scope for lack of a live feature** — every planned top-level feature is live. Only the
narrow items under *Deferred work* below remain. Do not scaffold, stub, or "prepare" for them without an
explicit task.

### Deferred work — DO NOT build without an explicit task

- **AMD GPU power — written but NEVER VERIFIED on hardware.** `AmdGpuSensorReader` reads `PMLOG_BOARD_POWER`
  (sensor 73) on adapters ADL reports as discrete, and has produced a reading **exactly zero times**: no
  discrete Radeon was available, and the only AMD part on the development machine is an iGPU, which the
  discrete gate correctly excludes. **First job for anyone with a discrete Radeon: check the tile against a
  known-good reading** (the vendor's own overlay, HWiNFO, or a wall meter). Two decisions inside it are
  deliberate and must not be "simplified":
  - The **discrete gate** (`ADL2_Adapter_ASICFamilyType_Get`, verified — it reports INTEGRATED|FUSION for the
    iGPU and errors for non-AMD adapters) exists because integrated parts report *package* power: measured
    against a pure CPU load, `GFX_POWER` climbed to ~50 W while `INFO_ACTIVITY_GFX` stayed pinned at 0 %, and
    `ASIC_POWER` swung erratically between 0 and 64 W on an idle part.
  - There is **no `ASIC_POWER` fallback**, on purpose. On older discrete cards it is *chip* power excluding
    some rails — it would look plausible, understate real draw, and not mean the same thing as the NVIDIA
    tile beside it. A board that doesn't report `BOARD_POWER` shows "—". (Note the converse trap: newer cards
    report **0** for `ASIC_POWER`, which is why upstream projects moved to `BOARD_POWER`.)
- **Intel GPU sensors** — no Intel adapter and no `igcl64.dll` were available, so the IGCL path was never
  written. Intel adapters fall through to no reader and show "—" for both tiles, which is the designed
  behaviour, not a bug. Adding it means a new `IGpuSensorReader` implementation and nothing else.
- **GPU temperature on the Dashboard card caption** — would append `· <temp>°C` to the GPU card's caption.
  The *data* is no longer the obstacle (the Performance tab reads it live); this is simply Dashboard UI work
  that has not been asked for.

### Multi-GPU — SHIPPED (branch `multiplePerformanceCards`, 2026-07)

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

## Strict Working Boundaries

You are only permitted to read and modify:

1. The current feature folder(s) listed under **Current Scope** above.
2. The design document (see below).
3. The default window code (the app's main/root window as originally scaffolded).

You may **read** other parts of the repo for context if needed to keep things consistent
(e.g. shared styles, naming conventions), but you should not **edit** anything outside the
three categories above without the user explicitly asking you to expand scope.

Do not:
- Create folders for features not listed under Current Scope.
- Refactor or "improve" unrelated feature folders while working on the active one.
- Modify project-wide config, build files, or dependencies unless the task specifically requires it and the user has confirmed it.

If a task seems to require touching something outside these boundaries, stop and ask the
user before proceeding.

Before performing any of the following, stop and ask first:
- moving files
- renaming folders
- changing namespaces
- changing architecture
- introducing new dependencies
- changing MVVM approach
- altering project structure

## Design Document

There is an attached design document describing UI/UX intent, layout, and behavior for
each feature. You may read this document as part of feature work on the current
feature(s).

## Folder Structure

Source lives under `DashDetective/src/`, split into three areas: shared building blocks,
the application shell, and one folder per feature ("tab"). Only Dashboard and Settings
currently exist.

```
/DashDetective
  Program.cs, App.axaml(.cs), app.manifest, Assets/   (bootstrap — project root)
  /src
    /Shared                     (cross-cutting, feature-agnostic)
      ViewModelBase.cs
      ISelfScrollingPage.cs   (marker: a page that fills the viewport and scrolls its own panes, so
                               the shell must NOT wrap it in a scroll region — see File Explorer)
      IRefreshablePage.cs     (marker: a page the toolbar Refresh routes to; Refresh() re-reads its
                               data — Dashboard re-samples, File Explorer reloads the current folder)
      ILiveSamplingPage.cs    (marker: a page with live sampling the toolbar Live pill pauses/resumes;
                               MainWindowViewModel.ToggleLive routes SetLive() over every nav page)
      IActivatablePage.cs     (marker: a page that should only work while it is on screen.
                               MainWindowViewModel.UpdatePageActivity routes SetActive() over every nav
                               page — the current one when the window is visible, nothing otherwise, so
                               hiding to the tray idles the app rather than merely hiding it. The five
                               sampling pages implement it; Hardware/Toolkit/Settings/File Explorer own
                               no timer and do not)
      SamplingGate.cs         (composes ILiveSamplingPage's answer with IActivatablePage's into the one
                               a page's timers care about, so the five do not each hand-roll the pair.
                               STARTS live BUT NOT ACTIVE — this is what makes a tab that is never
                               opened cost nothing, and why page constructors must build their timers
                               STOPPED. Fires its callback only on a TRANSITION, so a re-selected tab or
                               a pill toggled off-screen cannot churn the timers)
      HardwareNameFormatter.cs (static: trims vendor/marketing decoration from CPU/GPU names for the
                                compact captions; shared by Dashboard + Performance. Distinct from
                                HardwareCatalog.Normalize — display trim, not a lookup key)
      CollectionReconciler.cs  (generic keyed diff of an ordered snapshot into an ObservableCollection —
                                drop/update/move/insert in place, no flicker; shared by the Network
                                connections table + the Processes list)
      TrayIntegration.cs       (whether closing may hide to a tray icon rather than exit. WINDOWS ONLY:
                                stock GNOME runs no StatusNotifierItem host, and the setting is ON BY
                                DEFAULT, so honouring it there hides the window behind an icon that never
                                appears. Nothing can be asked at startup, and guessing wrong strands the
                                app — read by MainWindowViewModel.ShowInTray and the Settings toggle.
                                The FIRST hide additionally shows the tray notice — see /Shell/TrayNotice)
      GpuMetricsSupport.cs     (whether reading NVIDIA GPU utilization costs a helper process here.
                                LINUX ONLY: there the figure exists solely through nvidia-smi, which is
                                why the setting is opt-in at all; Windows takes it from a PDH counter it
                                already polls, so the toggle has nothing to turn on and the sampler
                                discards the write. The TrayIntegration shape — one named capability,
                                read by SettingDescriptions.NvidiaGpuMetricsFor and by
                                SettingsViewModel.CanUseNvidiaMetrics)
      /Charts
        MetricHistory.cs        (THE rolling buffer type: a double[window] plus HOW MUCH OF IT IS REAL, and
                                 the canonical shift (left by one, append at the end) that MetricChannel
                                 used to hold. Every page's histories and MetricChannel's own are these —
                                 do not declare a bare double[]. The fill count is what lets Points() plot
                                 only the samples taken, so a trace enters at the right edge and grows
                                 leftward instead of drawing a zero-filled buffer as measured idle)
        SparklinePoints.cs      (renders a rolling metric history to a Sparkline "x,y" points string on a
                                 fixed 0–100 axis; percentage metrics pass valueMax 100, unbounded ones a
                                 rolling peak. The `filled` overload emits only the newest slots AT THEIR
                                 REAL INDICES — Sparkline takes its x scale from the data's own maximum, so
                                 that alone right-anchors a partial trace. Reached through
                                 MetricHistory.Points, which supplies the count)
        ChartScale.cs           (peak/headroom/floor auto-scaling for the network throughput axis —
                                 Peak / FitPeak / FitAxis; shared by Dashboard + Performance + Network.
                                 Takes ReadOnlySpan<double>, so callers pass MetricHistory.Values)
        ChartAxis.cs            (where a chart's axis text sits + what an auto-scaled axis says. Gutter /
                                 Footer RESERVE NOTHING when there are no labels, which is what keeps every
                                 unlabelled chart measuring as it did before axis text existed; PlotRect
                                 gives a reservation up rather than inverting on a control too small for it;
                                 GridLine snaps a grid line to the half-pixel AND clamps it inside the plot
                                 — an edge line drew half outside a chart that does not clip, bleeding into
                                 the padding of whatever hosted it; RateLabels builds the three value labels
                                 for a throughput axis. Pure geometry: text is MEASURED by the control,
                                 which alone has the typeface, and COMPOSED here, which is what keeps the
                                 layout rules testable with no render backend)
        ChartStatus.cs          (the cold-start wording, one place for the four pages that show it. Clears
                                 as soon as a chart has TWO samples — enough to draw a line — not when the
                                 window finally fills: a trace growing in from the right already says data
                                 is arriving, and the label sits on the plot it would be describing)
      /Styles
        Palette.axaml           (colour brushes; merged in App.axaml. Light/Dark live in
                                 ResourceDictionary.ThemeDictionaries; accent + chart-series keys
                                 sit top-level and are swapped at runtime — see Theming below)
        SharedStyles.axaml      (reusable class styles: card, panel, seg, toggle, buttons,
                                 paneSplitter (draggable divider between resizable panes)…)
      /Controls
        Sparkline, StatCard, ChartLegend, InfoRow
                                       (reusable widgets; Sparkline auto-fits to its data
                                        by default, or set YMin/YMax for a fixed axis —
                                        StatCard forwards YMin/YMax to its inner sparkline.
                                        Fixed-axis mode also supports an optional second series
                                        (Points2/Stroke2) + gradient area fill (Fill), used by the
                                        Network throughput panel for download+upload on one scale,
                                        a background lattice (ShowGrid), and OPT-IN AXIS FURNITURE:
                                        AxisMaxLabel/AxisMidLabel/AxisMinLabel down the left,
                                        AxisStartLabel/AxisEndLabel along the bottom, and StatusText
                                        over the plot. Each reserves room ONLY when set, so adding
                                        them to one chart cannot resize another. Render works against
                                        a plot Rect from ChartAxis and draws the grid + labels BEFORE
                                        the has-enough-points bail-out, so an empty chart still states
                                        its scale. All of this is fixed-range mode only.
                                        ChartLegend is a chart's key: up to two series, each a swatch
                                        in its own colour beside its name; an entry with no label takes
                                        no room, so the same control serves a single-series chart.
                                        InfoRow is a key/value row; long values wrap to multiple
                                        lines (flush-right) instead of clipping — see SharedStyles infoVal)
    /Services                   (cross-cutting app services)
      /Settings
        AppSettings.cs          (immutable persisted-preferences record + Defaults; schemaVersion field)
        SettingsStore.cs        (load-on-start soft-fail to defaults; debounced atomic save to
                                 %AppData%/DashDetective/settings.json; Flush on shutdown. Pure
                                 persistence — knows no view-models; the composition root applies/observes)
        SettingsJsonContext.cs  (System.Text.Json source-gen context for AppSettings; string enums)
      /Startup
        IStartupRegistration.cs (the seam + ForCurrentPlatform(); see Provider seams below)
        WindowsStartupRegistration.cs
                                (HKCU …\Run add/remove for "Launch at startup"; Microsoft.Win32.Registry,
                                 soft-failing. Holds UnsupportedStartupRegistration too — reports
                                 "not enabled" and ignores writes off Windows)
        LinuxStartupRegistration.cs
                                (the XDG counterpart: ~/.config/autostart/DashDetective.desktop, honouring
                                 XDG_CONFIG_HOME when it is absolute. IsEnabled READS THE FILE, not just
                                 its existence — the spec disables an entry with Hidden=true, and some
                                 desktop tools write that instead of deleting)
        DesktopEntry.cs         (the .desktop body as pure statics. Exec is QUOTED and its four reserved
                                 characters escaped: unquoted, a path under /home/My User/ parses as two
                                 arguments and launches the wrong thing)
      /Diagnostics
        Log.cs                  (minimal soft-failing logger → Debug output + a per-day rolling file in
                                 %LocalAppData%/DashDetective/logs; never throws. The sampler / provider /
                                 MetricChannel catch blocks route through Log.Warn, and Program.cs hooks
                                 AppDomain.UnhandledException + TaskScheduler.UnobservedTaskException →
                                 Log.Error. No logging packages)
      /Theming
        ThemeService.cs         (single seam that applies theme + accent to Application at runtime. Also
                                 the brush seam for a page that assigns colours in code rather than through
                                 {DynamicResource} — BrushFor(ChartSeries), cached per palette, plus a
                                 SeriesChanged event so that page can re-resolve. Only the Performance tab
                                 needs it; everything else binds the resource keys)
        AppTheme.cs             (enum: System / Light / Dark)
        AccentPreset.cs         (record: one accent's Color/Hover/OnAccent/Deep; .All = the four)
        ChartPalette.cs         (THE source of every chart series colour, for the default look and for each
                                 accent, plus the ChartSeries enum and the ChartSeriesColors record.
                                 An accent ROTATES the palette rather than flattening it: the accent is the
                                 CPU (and net-down) series, and every other series keeps its own saturation
                                 and lightness while its hue turns by the accent's offset from the default
                                 blue. Derive(AccentPreset.Default.Color) reproduces Default exactly, which
                                 is what makes the blue swatch and the "Default" swatch agree. Pure HSL
                                 maths over Avalonia.Media value types — no render backend, so it is
                                 unit-testable)
      /Platform
        /Linux
          IProcFileSystem.cs    (the /proc + /sys read seam — Exists/ReadAllText/ReadAllLines/
                                 ListDirectory/ResolveLink, all never-throwing and empty-on-miss.
                                 Infrastructure, not a provider seam, so it sits in its own Services
                                 folder like IUiTimer. ProcFileSystem is the real one; the tests' fake
                                 is what makes every Linux provider testable from a Windows box.
                                 See "/proc access" below for the Path.Combine rule)
          ProcFileSystem.cs
          ProcStatParser.cs     (/proc/stat cpu-line format knowledge, shared by the aggregate and
                                 per-core samplers: label + busy/total jiffies, read by index with a
                                 length check. steal counts as busy; guest/guest_nice are excluded
                                 because the kernel already folds them into user/nice)
          ProcMeminfoParser.cs  (/proc/meminfo format knowledge, shared by the memory sampler and the
                                 system-counters provider: "Key: value kB" → a byte-valued lookup. The
                                 kB unit is KIBIbytes, so suffixed values scale by 1024 (saturating);
                                 unitless values are counts and stay verbatim. Absent key → 0)
          ProcCpuinfoParser.cs  (/proc/cpuinfo format knowledge, shared by CpuFacts and the frequency
                                 sampler: one key/value block per logical processor, split on blank
                                 lines. Keys are trimmed around the colon — the file separates them
                                 with a VARYING number of tabs, so a fixed-column split passes a
                                 hand-written fixture and fails on a real machine. Keys are prose, so
                                 lookups are OrdinalIgnoreCase. Absent key → "")
          CpuFacts.cs           (the derivation on top of ProcCpuinfoParser, shared by the Dashboard's
                                 CPU tile and the Hardware tab's Processor card so the two cannot
                                 disagree: name, physical/logical cores, max clock. Physical cores =
                                 distinct (physical id, core id) PAIRS — block count over-reports on a
                                 hyperthreaded chip and core id alone merges sockets. Max clock prefers
                                 the highest cpuinfo_max_freq across cores (not cpu0's — that is a
                                 little core on big.LITTLE), else the model name's "@ 3.60GHz". cpu MHz
                                 IS NEVER USED for it: it is the instantaneous clock, so a scaling
                                 governor would report an idle 800 MHz under a "max" label. Reports ""
                                 and 0 honestly; each consumer applies its own placeholder.
                                 L3CacheKilobytes is a separate static, not a field — only the Hardware
                                 card has a row for it. Sysfs writes that size SUFFIXED ("8192K", "16M"),
                                 never as bytes)
          ProcPids.cs           (the live PIDs, from /proc's all-digit entries. A shared DERIVATION, not a
                                 parser: the Performance tab's process count and the Processes tab's full
                                 walk both start here, so what counts as a process is decided once. Empty
                                 means the listing failed, never that the machine is idle)
          ProcPidStatParser.cs  (/proc/[pid]/stat format knowledge — NOT ProcStatParser, which is the
                                 machine-wide /proc/stat. Yields comm, state char, parent PID, utime+stime
                                 and num_threads, so one open covers most of a process row. THE comm FIELD
                                 IS PARENTHESISED AND MAY HOLD SPACES AND PARENTHESES — "(Web Content)",
                                 "(a (b) c)" — so it splits on the LAST ')'. A whole-line Split(' ') lands
                                 on the wrong token for every field after the name, and does it for exactly
                                 the processes users care about. Read by index behind an 18-token minimum;
                                 anything shorter is a torn read and the process is skipped)
          ProcPidStatusParser.cs
                                (/proc/[pid]/status, for the two fields stat cannot supply: the real uid and
                                 VmRSS. PPid, Threads and State are in this file too and are DELIBERATELY
                                 NOT read — stat is already open, and one number should have one source.
                                 The Uid line carries FOUR values (real, effective, saved-set, filesystem)
                                 and only the first is the owner. An unknown uid is null, NOT 0: 0 is root,
                                 and a denied read must never promote a user process into the System group.
                                 A missing VmRSS is 0 bytes — a kernel thread has no address space, and
                                 requiring the field would drop every kworker from the list)
          ProcPidIoParser.cs    (/proc/[pid]/io, for the Disk column. rchar + wchar, NOT read_bytes +
                                 write_bytes: the Windows column is ReadTransferCount + WriteTransferCount,
                                 which counts bytes through the syscall layer including cache, and rchar/
                                 wchar are that same measurement. Mode 0400 — readable for your own
                                 processes, denied for root's and other users', which is a blank rate)
          ProcCgroupParser.cs   (/proc/[pid]/cgroup, the input to LinuxProcessClassifier. Every line is
                                 hierarchy-ID:controllers:path and the unified v2 hierarchy is the one with
                                 ID 0 AND AN EMPTY CONTROLLER LIST — a hybrid v1/v2 host lists a dozen v1
                                 controllers alongside it, so taking the first or last line yields a v1
                                 path. Leaf() splits the last segment, which two classifier rules match on.
                                 A v1-only host has no unified line at all and yields "")
          ProcMountsParser.cs   (/proc/mounts format knowledge, used by LinuxVolumeProvider: space-separated
                                 device / mountpoint / fstype, read by index with a length check. The device
                                 and mount-point fields are OCTAL-ESCAPED (\040 = space, \134 = backslash),
                                 because the separator is a space — read raw, a literal "\040" shows up in
                                 the Partitions table for most removable media. A malformed escape is left
                                 as written. UnescapeUdev handles the DIFFERENT \xNN hex convention the
                                 /dev/disk/by-label symlink names use for the same job)
          ProcDiskstatsParser.cs
                                (/proc/diskstats format knowledge, used by the throughput sampler. Keyed by
                                 the PACKED major:minor of columns 1-2 — the same identity SysBlockFacts
                                 derives, which is what lets an independently-ticking sampler agree with the
                                 drive cards. Read by index behind a 14-FIELD MINIMUM: the row grew to 18
                                 fields in 4.18 (discards) and 20 in 5.5 (flushes), so only the first
                                 fourteen may be assumed. Sectors are 512 bytes, as in /sys/block/*/size)
          SysBlockFacts.cs      (the /sys/block derivation, shared by the Storage tab's drive cards, its
                                 Partitions table and the Hardware tab's Storage Devices card — the CpuFacts
                                 shape applied to disks. THE JOIN KEY FOR THE WHOLE STORAGE SURFACE is
                                 Pack(major, minor) = (major << 20) | minor, read from /sys/block/*/dev:
                                 PhysicalDiskInfo, VolumeInfo and DiskThroughputSample are all keyed by an
                                 int disk number that Windows gets from the OS, and Linux has no such
                                 number, so three independently-sampled providers derive one from the
                                 kernel's own device identity. A positional index would drift the moment a
                                 USB drive is plugged in mid-run. FILTERS loop*/ram*/zram*/sr* — a stock
                                 Ubuntu GNOME install has ~25 snap loop devices and without this the Storage
                                 tab is unusable. dm-*/md* are RESOLVED, NOT DROPPED: anything with a
                                 slaves/ entry is followed (to a depth cap, for LUKS-over-LVM) to the disk
                                 backing it, so an LVM root still lands on a real card. Partition→disk needs
                                 no symlink resolution — the kernel nests partitions inside their disk and
                                 prefixes them with its name. size is in 512-BYTE SECTORS regardless of the
                                 drive's physical sector size. DiskNumbers() is the cheap half, for the
                                 sampler, which runs every tick and only needs to tell a disk from a
                                 partition; DiskNumberOf() is the single-read name→number path, for a
                                 caller that already knows the device name — WHOLE DEVICES ONLY, since a
                                 partition's dev file carries the partition's number)
          DrmCardFacts.cs       (the /sys/class/drm derivation, shared by the adapter enumeration, the
                                 utilisation sampler, the sensor reader and the Hardware Graphics card —
                                 the CpuFacts shape applied to GPUs. THE JOIN KEY FOR THE WHOLE GPU SURFACE
                                 is Key = the card's PCI ADDRESS, standing in for a DXGI LUID: the inventory
                                 keeps only adapters the enumeration AND the sampler both report, so two
                                 readers deriving keys separately means no GPU card at all, silently.
                                 COUNTS cardN ONLY — /sys/class/drm mixes cards with renderD* nodes and one
                                 entry per connector (card0-DP-1), and counting those turns one GPU into
                                 four. SKIPS a node with no PCI vendor: no ids, no name, nothing to show.
                                 PCI id files carry an 0x PREFIX that NumberStyles.HexNumber REJECTS — miss
                                 the strip and every id reads 0. IsSoftware flags ONLY simpledrm/vkms: unlike
                                 DXGI's flag, a paravirtualised GPU IS the VM's real display adapter and
                                 hiding it leaves the VM with no GPU card at all. Bundled vendor table +
                                 driver name gives "AMD amdgpu (1002:73df)", degrading to raw hex)
          ProcNetParser.cs      (/proc/net/{tcp,tcp6,udp,udp6} format knowledge, used by
                                 LinuxConnectionsInterop. ONE parser for all four files: they share the ten
                                 leading columns (sl, local, remote, st, queues, timer, retrnsmt, uid,
                                 timeout, inode), so only the trailer differs. ADDRESSES ARE HEX 32-BIT
                                 WORDS IN HOST BYTE ORDER, NOT NETWORK ORDER — 0100007F is 127.0.0.1, and
                                 an IPv6 address is FOUR such words reversed INDEPENDENTLY; reversing all
                                 sixteen bytes instead puts a ::ffff: marker at the wrong end and yields a
                                 plausible but wrong global address. Ports sit in the same field and must
                                 NOT be swapped. State passes through as the KERNEL's code — translation is
                                 the interop's job. The sl header survives a column count (twelve fields)
                                 and is dropped by failing to decode as an address)
          SocketInodeMap.cs     (socket inode → owning PID by walking /proc/[pid]/fd for socket:[N] — the
                                 only rootless attribution, since /proc/net names an inode and never a PID.
                                 STATEFUL AND CACHED on purpose: the walk is a readlink per descriptor
                                 across every process and the connections table polls at 2.5 s, so it only
                                 walks when asked about an unseen inode. A SHARED SOCKET RESOLVES TO THE
                                 LOWEST PID, not the first one walked — /proc listing order is unspecified,
                                 and the row's identity key carries the PID, so an unstable choice would
                                 break the UI's keyed diff. Reads the inode by locating the socket:[ marker
                                 ANYWHERE in the link target, because ResolveLink returns a full path
                                 (/proc/1/fd/socket:[N]) rather than the bare target. An unlistable fd
                                 directory is the natural other-user filter, at one call per process)
          ProcPidName.cs        (the cmdline→comm name derivation, shared by the Processes tab and the
                                 Network tab's connection owners — the CpuFacts shape applied to names, so
                                 the same process cannot read systemd-resolved on one tab and the 15-char
                                 truncated systemd-resolve on the other. cmdline holds NUL-separated args,
                                 so argv[0] ends at the first NUL. Reports "" for a process that names
                                 itself nowhere, because the consumers' placeholders differ — Processes
                                 wants "Unknown", Network wants "PID 1234". No .exe is appended)
          IVolumeCapacityReader.cs
                                (a mounted filesystem's total/free bytes by mount point, over DriveInfo —
                                 the managed statvfs. A seam of its own beside IProcFileSystem for the same
                                 reason: /proc/mounts carries no sizes and statvfs is not a pseudo-file, so
                                 without it LinuxVolumeProvider could not be tested until someone ran the
                                 VM. Free is TotalFreeSpace, not AvailableFreeSpace — only that makes the
                                 cards' used figure agree with df, and it is what the WMI arm's
                                 SizeRemaining means)
          OsReleaseParser.cs    (/etc/os-release format knowledge: KEY=value into a lookup. The file is
                                 a shell fragment, so the same body mixes quoted and bare values — one
                                 MATCHED pair of surrounding quotes is stripped and an unbalanced one is
                                 left alone. Splits on the first = only. Absent key → "")
          DmiIdReader.cs        (the one-line files under /sys/class/dmi/id, shared by the Dashboard's
                                 System Information panel and the Hardware tab's Motherboard card.
                                 EXPOSES ONLY THE WORLD-READABLE KEYS as named properties —
                                 product_uuid, board_serial and product_serial are mode 0400 and are
                                 deliberately not offered, so no caller can depend on a value that
                                 silently reads "" for every non-root user. Carries Join (the DMI
                                 counterpart to WmiRead.Join) and Year, which reads SMBIOS's MM/DD/YYYY
                                 from the END — the opposite side from WmiRead.DmtfYear's yyyymmdd)
      /SystemMetrics
        IProcessorFrequencySampler.cs (seam + ForCurrentPlatform() + the ProcessorClockSample record,
                                 which carries EITHER a ratio or an absolute clock — Windows PDH reports
                                 % of base clock, Linux reports MHz directly and has no dependable base
                                 to divide by. Page-local to the Performance tab's CPU Speed tile)
        WindowsProcessorFrequencySampler.cs
                                (live CPU current-clock ratio via PDH
                                 \Processor Information(_Total)\% Processor Performance — × the WMI base
                                 clock, like Task Manager. NOT the % Processor Utility counter, which is
                                 utilisation, not clock speed. Holds UnsupportedProcessorFrequencySampler)
        LinuxProcessorFrequencySampler.cs
                                (cpufreq scaling_cur_freq averaged over the online cores, falling back to
                                 /proc/cpuinfo's cpu MHz — which is what a VM usually has, since cpufreq
                                 is typically absent under VirtualBox)
        ILogicalProcessorSampler.cs (seam + ForCurrentPlatform() + the LogicalProcessorSample record;
                                 per-logical-processor % for the Performance tab's CPU "Detailed" view)
        WindowsLogicalProcessorSampler.cs
                                (per-core % via the PDH \Processor Information(*)\% Processor Utility
                                 array, _Total roll-ups dropped. Holds UnsupportedLogicalProcessorSampler)
        LinuxLogicalProcessorSampler.cs
                                (per-core % from /proc/stat's cpu0..cpuN. /proc/stat lists ONLINE cpus
                                 only, so state is keyed by core number and a core appearing mid-run
                                 reports 0 until it has an interval — never its since-boot average)
        ICpuSampler.cs          (seam over the total-CPU readers + UnsupportedCpuSampler)
        CpuUsageSampler.cs      (live total CPU %; its public ctor is the ONE place the CPU reader's
                                 platform is chosen — LinuxCpuSampler, else the PDH-then-GetSystemTimes
                                 chain on Windows, else Unsupported)
        LinuxCpuSampler.cs      (total CPU % from /proc/stat's aggregate line. STATEFUL — holds the
                                 previous jiffy snapshot, so it must not go into HardwareProviders)
        IMemoryUsageSampler.cs  (seam + ForCurrentPlatform() + the MemorySample record; live RAM % +
                                 used/total + the commit pair, feeding the shared Memory metric)
        WindowsMemoryUsageSampler.cs
                                (GlobalMemoryStatusEx. Latches inert in Sample() rather than a ctor, since
                                 there is no query to stand up. Holds UnsupportedMemoryUsageSampler)
        LinuxMemoryUsageSampler.cs
                                (/proc/meminfo; used = MemTotal − MemAvailable, the closest analogue to
                                 the Windows load figure and what `free -h` calls available. Falls back to
                                 MemFree + Cached + Buffers on pre-3.14 kernels. Committed_AS/CommitLimit
                                 pass through UNCLAMPED — overcommit legitimately exceeds the limit)
        ISystemPerformanceProvider.cs
                                (seam + ForCurrentPlatform() + the SystemPerformanceSample record: file
                                 cache + process/thread/handle totals, shared by the Performance tab's CPU
                                 and Memory panes. EVERY member is nullable — a platform with no analogue
                                 reports null, which the tiles render "—")
        WindowsSystemPerformanceProvider.cs
                                (psapi GetPerformanceInfo — all four figures from one call, no PDH counter
                                 and no per-tick process enumeration. Holds
                                 UnsupportedSystemPerformanceProvider)
        LinuxSystemPerformanceProvider.cs
                                (cache = Cached + SReclaimable; threads = /proc/loadavg's nr_threads (the
                                 DENOMINATOR of field 4 — it is threads, not processes); processes =
                                 /proc's numeric entries, one listing and no per-PID opens. HANDLES ARE
                                 PERMANENTLY "—": a Windows handle covers events, threads and registry keys
                                 too, so /proc/sys/fs/file-nr would mean something else under the label)
        IGpuUsageSampler.cs     (seam: SampleAdapters() only. ITS KEYS MUST MATCH IGpuAdapterProvider'S —
                                 DeviceInventory intersects the two, so an arm that derives the adapter key
                                 differently from its enumeration counterpart yields NO GPU AT ALL rather
                                 than a wrong one, with every individual reading still looking fine. Also
                                 carries NvidiaMetricsEnabled as a DEFAULT interface member (get => false),
                                 so only the one arm with a spawn-costing source pays for the setting)
        WindowsGpuUsageSampler.cs
                                (live GPU % via PDH GPU Engine counters; owns a PDH query handle.
                                 SampleAdapters() = per-physical-GPU split keyed by adapter LUID token, and
                                 the whole surface: the combined Sample()/SampleEngines() pair the multi-GPU
                                 split replaced has been removed. Page-local per tab — the Dashboard cards +
                                 Performance rows each own one)
        LinuxGpuUsageSampler.cs (amdgpu gpu_busy_percent per card, keyed by the shared DrmCardFacts.Key.
                                 EVERY ADAPTER IS REPORTED, with a NULL Overall where the driver publishes
                                 no figure — omitting one would delete its card entirely, and a 0 would show
                                 real hardware as permanently idle. NO ENGINE BREAKDOWN: sysfs has one
                                 scalar per card and the per-engine split is root-only debugfs, so the
                                 Performance tab's Detailed toggle stays hidden. Card list resolved once at
                                 construction; only the utilisation file is re-read per tick)
        NvidiaSmiReader.cs      (the only rootless NVIDIA utilisation source — the proprietary driver
                                 publishes nothing in sysfs. SPAWNS A PROCESS, so it is off the sampling
                                 path entirely: at most one run per 15 s of WALL CLOCK (not per N ticks —
                                 the tick interval is a user setting), never overlapping, never blocking,
                                 gated behind an off-by-default setting, and retired for the session on the
                                 first failure. NORMALISES THE BUS ID: nvidia-smi writes an EIGHT-digit PCI
                                 domain and uppercase hex where sysfs writes four and lowercase, so a raw
                                 join matches nothing and every reading silently fails to find its card.
                                 Runs over the Toolkit's IProcessLauncher seam, so tests spawn nothing)
        HardwareProviders.cs    (the "what hardware is in this machine" bundle + the single
                                 ForCurrentPlatform() that picks the Windows, Linux or unsupported set
                                 for all SEVEN members. The Linux arm is now complete except for per-DIMM
                                 memory, which stays Unsupported* for good (dmidecode needs root).
                                 It carries NO
                                 [SupportedOSPlatform]: the Linux readers are portable managed code over
                                 IProcFileSystem, so there is no annotated API for CA1416 to see.
                                 Built by each consuming page's public ctor (Dashboard,
                                 Performance, Storage) and handed to DeviceInventory.LoadAsync.
                                 EVERY MEMBER MUST BE STATELESS — it is constructed three times and its
                                 members run concurrently; stateful providers are deliberately excluded)
        IGpuAdapterProvider.cs  (seam + the GpuPciId / GpuAdapter records. GpuAdapter.FormatLuidToken —
                                 pure, unit-tested — lives on the record, not the DXGI reader.
                                 GpuAdapter.LuidToken IS NAMED FOR WINDOWS BUT IS NOT ALWAYS A LUID: it is
                                 whatever this platform uses to say "the same adapter" across independent
                                 readers — the PDH luid token on Windows, the card's PCI address on Linux)
        LinuxGpuAdapterProvider.cs
                                (/sys/class/drm enumeration over the shared DrmCardFacts, taking its Key
                                 rather than deriving one. Packs the two sysfs subsystem id files into the
                                 single field DXGI reports, so a card reads the same on both platforms)
        WindowsGpuAdapterProvider.cs
                                (DXGI adapter enumeration via raw vtable fn-pointers — LUID→name + software
                                 flag + VRAM; the authoritative LUID→name map for multi-GPU, async. Its
                                 DedicatedVideoMemory rides through DeviceInventory onto
                                 DeviceInstance.VramBytes → the Performance GPU VRAM tile)
        IPhysicalDiskProvider.cs / WindowsPhysicalDiskProvider.cs
                                (all-disks WMI enumeration; takes IDiskTemperatureProvider by ctor —
                                 ForCurrentPlatform shares ONE reader with the Storage page)
        LinuxPhysicalDiskProvider.cs
                                (the same cards from SysBlockFacts, taking IDiskTemperatureProvider the same
                                 way. HEALTH IS ALWAYS HEALTHY — SMART needs root and has no rootless
                                 near-miss. Temperature IS read, and unlike the Windows arm it is asked for
                                 on EVERY drive, not just NVMe: the source is hwmon, and drivetemp covers
                                 SATA/SAS too, so a media-kind gate here would make that path dead code)
        IVolumeProvider.cs / WindowsVolumeProvider.cs
                                (MSFT_Volume enumeration incl. unlettered Recovery/EFI. VolumeInfo carries
                                 BOTH DriveLetter and MountPoint — the platforms name the same thing
                                 differently and callers fall through from one to the other; a Windows
                                 volume leaves MountPoint empty and a Linux one DriveLetter null)
        LinuxVolumeProvider.cs  (/proc/mounts joined to SysBlockFacts, sized through IVolumeCapacityReader.
                                 ONE FILTER RULE does the whole job: keep a mount only when its device
                                 resolves to a disk that has a card. tmpfs/cgroup/proc name no /dev device
                                 and every snap mount resolves to an excluded loop, so both floods fall out
                                 and no volume points at a cardless disk — a filesystem allowlist would be a
                                 weaker second guess. DEDUPES BY RESOLVED DEVICE, shortest mount point
                                 winning: /proc/mounts lists one device many times (bind mounts, btrfs
                                 subvolumes) and StorageComposer SUMS a disk's volumes, so duplicates
                                 multiply its capacity. /dev/mapper and /dev/disk/by-uuid names are
                                 symlinks and are resolved. Labels come from the by-label symlinks)
        SystemVolume.cs         (which volume hosts the OS, and through it which disk — the Dashboard's
                                 Storage tile and the Storage tab's Disk Activity panel both need it and
                                 each used to hold its own copy. Tries SystemDrive.Letter, then the "/"
                                 mount point; only one arm can ever match on a given platform)
        IDiskTemperatureProvider.cs / WindowsDiskTemperatureProvider.cs
                                (NVMe composite temp via non-admin IOCTL health log. SYNCHRONOUS by
                                 design — called per-disk on a slow sub-tick of a timer the caller owns)
        LinuxDiskTemperatureProvider.cs
                                (temp1_input from the drive's hwmon, in MILLIDEGREES. MATCHED ON THE
                                 HWMON'S name, NEVER ITS INDEX — numbering is not stable across boots and
                                 the low ones are usually coretemp/acpitz, so an index read reports the CPU
                                 on a drive card. TWO WALKS reach the block device: an nvme hwmon hangs off
                                 the CONTROLLER with the device a namespace child (nvme0 → nvme0n1), a
                                 drivetemp one off a SCSI target with the device under block/. FINDING
                                 NOTHING IS THE COMMON CASE — drivetemp is not loaded by default on most
                                 distros; that is correct, not a bug. Stateless: re-walks per call)
        StorageUsageSampler.cs  (live disk Active time % + read/write/response via PDH PhysicalDisk
                                 counters; owns a PDH query handle)
        IPhysicalDiskThroughputSampler.cs
                                (seam + the DiskThroughputSample record + ForCurrentPlatform(). Unlike the
                                 HardwareProviders members this is deliberately STATEFUL — every arm reports
                                 the interval since the previous call — so each page owns its own instance.
                                 Holds UnsupportedPhysicalDiskThroughputSampler's contract; the twin itself
                                 sits under the Windows arm)
        WindowsPhysicalDiskThroughputSampler.cs
                                (per-disk read/write/active/response/queue from the PDH \PhysicalDisk(*)
                                 counter ARRAY — deliberately not the _Total instance, whose % Idle Time is
                                 a mean across every disk. Holds the Unsupported twin)
        LinuxPhysicalDiskThroughputSampler.cs
                                (the same five figures from /proc/diskstats, diffed over a Stopwatch
                                 interval like NetworkUsageSampler. ACTIVE TIME COMES FROM io_ticks
                                 (milliseconds with a request outstanding) — the direct analogue of
                                 100 − % Idle Time, and what every headline number and sparkline on the
                                 Storage tab and the Dashboard's disk cards renders; without it they all
                                 read a flat zero. Response = Δ(ms reading + ms writing) over Δ completed
                                 transfers; queue depth is INSTANTANEOUS, not a delta. REPORTS WHOLE DISKS
                                 ONLY — /proc/diskstats lists sda and sda1 alike and their I/O overlaps, so
                                 counting both roughly doubles every figure. A counter that goes backwards
                                 (device re-plugged) reads as no activity)
        MetricChannel.cs        (reusable "sampler + DispatcherTimer + rolling MetricHistory"
                                 unit — one try/catch per tick → onFailed + permanent stop; SampleNow for
                                 paused Refresh. Non-generic MetricChannel for plain-double metrics,
                                 generic MetricChannel<TSample> for snapshot samples + a no-history variant)
        SystemMetricsService.cs (SINGLE owner of the four SHARED samplers — CPU, Memory, Storage, Network;
                                 per-metric 1 Hz channel fans each sample out to subscribers (ref-counted — a
                                 channel runs only while it has one), Pause/Resume for the Live pill,
                                 RefreshAll for Refresh, per-metric fault isolation. Dashboard / Performance /
                                 Processes SUBSCRIBE instead of owning these samplers. Per-GPU and per-disk
                                 readings are page-local instead (multi-instance; this feed carries only a
                                 single aggregate), as is the Network tab's own NetworkUsageSampler. Built in
                                 the App composition root and disposed on shutdown.
                                 THE ALERT WATCHER IS OPT-IN (AlertsEnabled, mirroring the "Resource alerts"
                                 setting and off by default like it): it subscribes to CPU + Memory, so left
                                 on it holds both channels sampling with every page deactivated — which is
                                 most of what the app used to cost hidden in the tray. Clearing it also
                                 clears any active alert, so a banner cannot outlive its setting.)
        MetricSubscriptions.cs  (a page's subscriptions as FACTORIES rather than tokens, with idempotent
                                 Attach/Detach, so IActivatablePage can drop and re-establish them.
                                 DROPPING THEM IS WHAT STOPS THE FEED — the service ref-counts subscribers,
                                 so a deactivated page that merely ignored its callbacks would still be
                                 paying for them. Re-attaching replays the cached latest sample, so a page
                                 returning to screen seeds with real data instead of a blank frame)
      /Network
        NetworkGateway.cs       (the machine's own IPv4 default gateway, over NetworkUsageSampler
                                 .SelectPrimary. Shared because two features want it: the Toolkit's
                                 parameterised ping/tracert boxes and the Network tab's ping panel.
                                 REPORTS null RATHER THAN A FALLBACK HOST — the two callers answer "no
                                 gateway" differently, and the lookup must not decide for them: the
                                 Toolkit substitutes a literal (a `ping <host>` row with an empty box is
                                 a dead button), while the Network tab leaves its box empty rather than
                                 offering a host the user never asked to contact. Never throws; slow
                                 enough that callers run it off the UI thread)
        NetworkUsageSampler.cs  (live down/up Mbps via managed NetworkInterface; samples ONE primary
                                 adapter — internet-facing, has a default gateway — NOT a sum of all
                                 adapters, see the gotcha below. Shared: Dashboard and the Network tab
                                 each own an instance, and the Network tab's AdapterInfoProvider reuses
                                 SelectPrimary() to identify the primary adapter — one source of truth.
                                 Moved here from src/Tabs/Dashboard with sign-off when the Network tab
                                 was activated.)
    /Shell                      (the app frame — the "default window")
      MainWindow.axaml(.cs), MainWindowViewModel.cs, ViewLocator.cs
                                (MainWindow's root is a DockPanel hosting the NavigationView at the
                                 user-chosen edge (DockPanel.Dock bound to Nav.Dock) + the main area.
                                 MainWindow's page-host is a Panel with two mutually-exclusive hosts:
                                 a scrolling ScrollViewer (ScrollingPage) and a bounded ContentControl
                                 (SelfScrollingPage), so ISelfScrollingPage pages self-scroll within
                                 the viewport — see File Explorer)
      /TrayNotice
        TrayNoticeWindow.axaml(.cs) (the ONE-TIME "this app is still running" dialog, shown before the
                                     FIRST hide-to-tray and never again (AppSettings.TrayNoticeShown).
                                     Asked BEFORE hiding, over the window the user has just closed: a
                                     toast afterwards would have to guess where the tray is, and can be
                                     missed. MainWindow.OnClosing cancels the close and awaits it from a
                                     helper, the ExportReportAsync split, since a closing handler cannot
                                     await. AskAsync returns bool? ON PURPOSE — with a plain bool a
                                     title-bar dismissal is default(bool) and would EXIT the app; the
                                     safe answer is the one the setting already gives. No view model:
                                     it holds no state but which button was pressed)
      /Navigation
        NavigationView.axaml(.cs)   (the collapsible/dockable nav-bar component; brand + item list +
        NavigationViewModel.cs       footer, with no permanent control chrome — collapse is the hover
                                     puck, re-docking is the right-click menu or the drag gesture. The
                                     VM owns Orientation + IsCollapsed and exposes all layout as computed
                                     properties — Dock, Rail sizes, ItemsOrientation, Hairline edge,
                                     scroll axis, puck geometry — no converters. Selection/layout visuals
                                     are styled in NavigationView.axaml via DynamicResource so they
                                     follow theme + accent)
        NavItem.cs, Icons.cs        (NavItem is a pure data model; Icons holds the glyph geometries)
        NavOrientation.cs           (enum: the dock edge — Left/Right/Top/Bottom)
        ChevronDirection.cs         (enum: which way the puck's chevron points. Split from the geometry
                                     so the rule is testable — Geometry.Parse needs a render backend,
                                     which the unit tests do not have, so touching Icons at all throws)
        NavPositionOption.cs        (selectable item VM for the dock menu, like NavItem/ThemeOption)
    /Tabs                       (one self-contained folder per feature)
      /Dashboard                DashboardView.axaml(.cs) + DashboardViewModel.cs
                                ICpuInfoProvider.cs + WindowsCpuInfoProvider.cs
                                                        (CPU info via WMI, async. Reached through the shared
                                                         HardwareProviders bundle, NOT statically)
                                LinuxCpuInfoProvider.cs (the same card from /proc/cpuinfo + cpufreq, via the
                                                         shared CpuFacts. Substitutes only the placeholder
                                                         name; physical cores and clock stay 0 → "—")
                                CpuStaticInfo.cs        (record for the result)
                                IMemoryInfoProvider.cs + WindowsMemoryInfoProvider.cs
                                                        (RAM info via WMI, async)
                                MemoryStaticInfo.cs     (record for the WMI result)
                                (the CPU/Memory/GPU/Storage/Network *samplers* now live under
                                 src/Services/SystemMetrics + /Network and are owned by
                                 SystemMetricsService — the Dashboard VM subscribes, it no longer owns them)
                                ISystemInfoProvider.cs + WindowsSystemInfoProvider.cs
                                                        (static system identity — OS/device/BIOS/board/build —
                                                         via WMI + registry, async; uptime is live off
                                                         Environment.TickCount64 in the VM, no sampler file)
                                LinuxSystemInfoProvider.cs
                                                        (the same panel from /etc/os-release (PRETTY_NAME, then
                                                         NAME+VERSION_ID, then OSDescription),
                                                         /proc/sys/kernel/osrelease for Build — the kernel
                                                         release is the analogue of the Windows build number —
                                                         and DmiIdReader for BIOS + board, which falls back to
                                                         the chassis fields laptops populate more reliably)
                                SystemStaticInfo.cs     (record for the system-identity result)
      /Toolkit                  ToolkitView.axaml(.cs) + ToolkitViewModel.cs
                                (the design doc's "Commands" tab. Filter bar (search box + category
                                 chips + count) over a grouped command list, beside a pinned 340px
                                 Execution Log. ISelfScrollingPage (each column scrolls itself) +
                                 IShortcutTarget. Running a row is an async RelayCommand that refuses
                                 concurrent runs and prepends one stanza to the log. View code-behind
                                 owns the search focus + the search-reveal flash, like
                                 ProcessesView/SettingsView. The row is a chrome-less Button so Enter
                                 works on a focused row natively — ShortcutId.Activate falls through
                                 MainWindowViewModel's global switch unconsumed, so no IShortcutTarget
                                 case is needed. The copy button is a SIBLING of it, not nested)
                                ToolkitCategory.cs      (enum: the four sections, declaration order =
                                                         display order)
                                ToolkitEntryKind.cs     (enum: Folder / App / Command / Panel / Link —
                                                         what a command opens, driving its icon + badge)
                                ToolkitEntry.cs         (immutable row model; its Icon/Badge* getters
                                                         resolve through ToolkitIcons ON READ, so the
                                                         filter/catalog tests never load geometry)
                                ToolkitGroup.cs         (immutable category section: upper-cased header
                                                         + the entries that survived the filter)
                                ToolkitCategoryOption.cs (filter-chip item VM, the FilterOption shape)
                                ToolkitLogEntry.cs      (record: Time / Command / Output — one console
                                                         stanza in the Execution Log)
                                ToolkitCatalog.cs       (static COPY table only — categories, section
                                                         headers, badge labels. Reads the same on every
                                                         platform, which is why it stayed static when the
                                                         command set did not)
                                IToolkitCatalog.cs      (the per-platform command set, three arms:
                                                         WindowsToolkitCatalog / LinuxToolkitCatalog /
                                                         UnsupportedToolkitCatalog. NO
                                                         [SupportedOSPlatform] on any of them — they are
                                                         string literals with no platform API surface,
                                                         and Instance is a static field initialised
                                                         outside the guard anyway)
                                WindowsToolkitCatalog.cs, LinuxToolkitCatalog.cs,
                                UnsupportedToolkitCatalog.cs
                                                        (the tables. Unsupported is EMPTY on purpose:
                                                         a platform with no table gets the page's own
                                                         empty state and can still author its own rows,
                                                         which beats thirty rows that can only fail.
                                                         EACH TABLE HAS EXACTLY ONE ELEVATED ROW —
                                                         sfc /scannow, fwupdmgr refresh — pinned by name
                                                         in that catalog's own tests, because a table is
                                                         the only place elevation can be authored at all)
                                ToolkitRows.cs          (the shared row factories — Folder/Tool/Panel/
                                                         Diagnostic/Doc. Both tables build through them,
                                                         so a category cannot drift away from the kind
                                                         and action it is supposed to pair with)
                                ToolkitActionKind.cs    (enum: OpenPath / OpenUrl / Launch / Capture /
                                                         Elevated — how a row is carried out. An enum,
                                                         not flags, so "elevated AND captured" cannot be
                                                         expressed: Windows forbids redirecting a runas
                                                         process's streams)
                                ToolkitAction.cs        (immutable: target + argument LIST + timeout,
                                                         built only via static factories. WithArgument
                                                         appends exactly one element, so a parameterised
                                                         entry's value can never split into a flag)
                                ToolkitRunner.cs        (THE single entry point for running a row. Never
                                                         throws — a missing tool, non-zero exit, timeout
                                                         or declined UAC prompt all become worded
                                                         failures. Expands env vars in the TARGET only;
                                                         refuses any OpenUrl that isn't https://)
                                ToolkitRunResult.cs     (record: Success / Output / ExitCode)
                                ToolkitOutputFormatter.cs (pure statics: stream merge, CRLF normalising,
                                                         console sign-off trim, 200-line / 16 KB caps with
                                                         the trim announced, plus the outcome wording)
                                IProcessLauncher.cs +   (the process seam + its real implementation — the
                                SystemProcessLauncher.cs only place in the app that starts a process.
                                                         Arguments go via ProcessStartInfo.ArgumentList,
                                                         never the joined string. Both output streams are
                                                         drained CONCURRENTLY and WITHOUT the timeout
                                                         token, or a command that floods its pipe
                                                         deadlocks and a killed one loses what it printed.
                                                         BuildLaunchInfo takes the platform as a parameter
                                                         so both elevation arms are testable from either
                                                         host: runas verb on Windows, pkexec as the file
                                                         name with the target as its first argument on
                                                         Linux — and UseShellExecute MUST be off there or
                                                         the launch goes to xdg-open, which cannot carry
                                                         arguments)
                                ToolkitFilter.cs        (pure statics: Matches (chip AND term, over the
                                                         command and its description) + Group (buckets
                                                         into catalog order, dropping emptied sections).
                                                         The ProcessFilter pattern)
                                ToolkitIcons.cs         (feature-local per-kind glyphs + fixed badge
                                                         tints, the HardwareIcons pattern)
      /Settings                 SettingsView.axaml(.cs) + SettingsViewModel.cs
                                                        (fully live: Appearance + Navigation + Monitoring
                                                         + Export & Data; view code-behind owns the
                                                         export save dialog + clipboard, like MainWindow)
                                ThemeOption.cs, AccentOption.cs, IntervalOption.cs
                                                        (selectable item VMs for the Appearance +
                                                         refresh-interval controls, like NavItem)
                                SettingDescriptions.cs  (the descriptions that name a MECHANISM rather
                                                         than an effect, or a platform that cannot honor
                                                         one, so cannot be shared — "Start with Windows",
                                                         the tray row's, and the NVIDIA row's. Each is the
                                                         label the page shows AND the text search matches,
                                                         so a wrong one is both misleading and unfindable.
                                                         Takes the platform as a parameter, the
                                                         ProcessGroupNames shape. KEYWORDS ARE NOT
                                                         per-platform: they are shared, so editing them
                                                         changes the Windows search index too. TWO rows are
                                                         DISABLED-NOT-HIDDEN on a platform that cannot
                                                         honor them (ShowInTray, NvidiaGpuMetrics): the
                                                         search index must not vary by platform, the search
                                                         reveal finds a row by its Tag IN THE VISUAL TREE
                                                         so a collapsed one is a dead hit, SettingCatalog
                                                         tests pin a catalog<->SettingId bijection, and a
                                                         settings.json carried between machines has to
                                                         survive. IsEnabled goes on the row BORDER, not the
                                                         toggle — a disabled toggle alone reads as an off
                                                         one)
      /FileExplorer             FileExplorerView.axaml(.cs) + FileExplorerViewModel.cs
                                                        (VM implements ISelfScrollingPage +
                                                         IRefreshablePage; owns filter, sort + ShowHidden
                                                         state and RebuildVisibleEntries; drives live
                                                         auto-refresh + scroll-to-top-on-navigation)
                                DirectoryService.cs     (async System.IO enumeration: lazy subdirectories,
                                                         folder entries; per-entry soft-fail, Task.Run off
                                                         the UI thread; takes includeHidden to reveal
                                                         hidden/system entries. FileItem carries raw
                                                         Size/Modified sort keys. GetEntriesAsync takes
                                                         IShellInterop and GetDrivesAsync takes
                                                         IFileSystemRoots — both per-platform questions are
                                                         asked of a seam. Still static: it holds no state
                                                         and is in no bundle. RootHasChildren is the one
                                                         chevron probe both roots providers share.
                                                         GetEntriesAsync returns a FolderRead — the items
                                                         PLUS a FolderReadStatus saying why an empty one is
                                                         empty. IgnoreInaccessible suppresses the failure to
                                                         open the FOLDER ITSELF, not just its children, so a
                                                         denied folder is otherwise a successful empty list;
                                                         Diagnose re-asks WITHOUT the suppression and ONLY
                                                         when the listing produced nothing, which keeps the
                                                         partial-list contract and costs an ordinary folder
                                                         nothing. Do not just turn IgnoreInaccessible off)
                                FolderMessages.cs       (why the file list is blank, worded: title + hint for
                                                         denied / gone / unreadable / hidden-only / empty /
                                                         filtered-to-nothing / no-folder-open. Pure and
                                                         render-free BECAUSE FileExplorerViewModel cannot be
                                                         tested — the FileExplorerPanes shape)
                                DirectoryWatcher.cs     (debounced FileSystemWatcher over the open folder;
                                                         raises Changed → VM auto-refreshes the list + tree.
                                                         Windows-guarded, soft-failing, app-lifetime)
                                FileSystemNode.cs       (tree-node item VM; lazy children on expand;
                                                         threads a Func<bool> includeHidden accessor;
                                                         SyncChildrenAsync reconciles a branch in place)
                                FileEntry.cs            (file-list row item VM; exposes raw Size/Modified)
                                FileSortKey.cs          (enum: Name / Type / Modified / Size)
                                SortColumn.cs           (clickable-header VM: Key + SortCommand + IsActive
                                                         + Arrow — same shape as FilterOption)
                                FileSizeFormatter.cs    (humanize bytes KB/MB/GB/TB; folders → "—")
                                FileTypeCatalog.cs      (extension → vector glyph + fixed colour. TOUCHES
                                                         AVALONIA — its Geometry.Parse initialiser needs a
                                                         render backend, so nothing testable may live here)
                                FileTypeDescriptions.cs (extension → friendly name, render-free precisely
                                                         because FileTypeCatalog is not. A table rather
                                                         than xdg-mime, which costs a subprocess per row.
                                                         Treats a leading dot as hidden, not an extension)
                                IShellInterop.cs        (seam + ForCurrentPlatform())
                                WindowsShellInterop.cs  (feature-local shell32 P/Invoke:
                                                         SHGetFileInfo type name + SHObjectProperties.
                                                         Holds UnsupportedShellInterop + the shared
                                                         ShellFallback. NOTE Open() stays live in BOTH —
                                                         it is managed Process.Start with UseShellExecute
                                                         and was never platform-guarded)
                                LinuxShellInterop.cs    (no interop at all: names from the table above,
                                                         Open via ShellFallback. NO PROPERTIES DIALOG
                                                         EXISTS — no desktop offers one to a foreign
                                                         process — so it reveals the containing folder
                                                         instead. RevealTarget is split from the launch so
                                                         it is testable without starting a file manager)
                                IFileSystemRoots.cs     (seam + ForCurrentPlatform(): what the tree's roots
                                                         are is the one per-platform question here)
                                WindowsFileSystemRoots.cs
                                                        (ready drives via DriveInfo, "Local Disk (C:)".
                                                         Holds UnsupportedFileSystemRoots — an EMPTY list,
                                                         which is what the old IsWindows() guard returned.
                                                         NO [SupportedOSPlatform]: DriveInfo, the
                                                         VolumeLabel getter and DriveType are unannotated,
                                                         so it would be decorative)
                                LinuxFileSystemRoots.cs (/, $HOME, and removable mounts from /proc/mounts
                                                         via ProcMountsParser. Matches the /media/,
                                                         /run/media/ and /mnt/ PREFIXES so no user name is
                                                         resolved — udisks2 uses the first on Ubuntu and
                                                         the second on Fedora/Arch)
      /Network                  NetworkView.axaml(.cs) + NetworkViewModel.cs
                                                        (VM implements IRefreshablePage + ILiveSamplingPage
                                                         + IActivatablePage; it polls only while it is the
                                                         visible tab. Owns the throughput sampler +
                                                         adapter/connection/ping timers and the keyed-diff
                                                         for the connections list. THE PING AND DNS PANELS
                                                         ARE USER-INITIATED and start nothing on their own —
                                                         see the write-up in the Appendix. Tab-local
                                                         MonoFont + fixed console-colour resources live in
                                                         the view — promote to Shared if reused)
                                NetworkProviders.cs     (the tab's provider bundle + ForCurrentPlatform();
                                                         see Provider seams above. TWO platform choices here
                                                         — which IConnectionsInterop and which
                                                         IProcessNameResolver — and the bundle makes neither:
                                                         each seam picks its own arm and the bundle takes
                                                         what it is handed. Everything else is portable)
                                IAdapterInfoProvider.cs (seam + the AdapterSnapshot record)
                                AdapterInfoProvider.cs  (async snapshot: all adapters + primary IP config
                                                         via managed NetworkInterface; per-adapter/field
                                                         soft-fail. No platform prefix — portable; the one
                                                         Windows-only field (DHCP) is guarded inline → "—")
                                AdapterInfo.cs          (record + AdapterKind enum; fixed status-dot brushes)
                                IpConfigInfo.cs         (record: IPv4/mask/gateway/DNS/MAC/DHCP; .Unknown)
                                IConnectionsInterop.cs  (seam + ForCurrentPlatform() + the RawConnection
                                                         struct. ADDRESS FAMILIES ARE WHATEVER THE PLATFORM
                                                         CAN SUPPLY, not a fixed set — Linux includes IPv6,
                                                         Windows is IPv4-only. State is a MIB_TCP_STATE value
                                                         whatever the platform's own numbering is)
                                WindowsConnectionsInterop.cs
                                                        (feature-local iphlpapi P/Invoke:
                                                         GetExtendedTcpTable/GetExtendedUdpTable, IPv4
                                                         OWNER_PID tables; port byte-order swap. IPv6
                                                         deferred — the OWNER_PID tables use different
                                                         16-byte-address structs. Holds
                                                         UnsupportedConnectionsInterop — reports none)
                                LinuxConnectionsInterop.cs
                                                        (/proc/net/{tcp,tcp6,udp,udp6} over ProcNetParser,
                                                         owners via SocketInodeMap. TRANSLATES THE KERNEL'S
                                                         TCP STATE CODES TO THE MIB NUMBERING — the two
                                                         tables are unrelated (Linux LISTEN 0x0A is MIB
                                                         Last-ack, Linux ESTABLISHED 0x01 is MIB Closed), so
                                                         passing them through labels every row wrongly AND
                                                         plausibly. 8→8 is a coincidence, not a shared code.
                                                         Reports UDP as connectionless to match the Windows
                                                         row shape, even for a socket the kernel tracks as
                                                         connected. No [SupportedOSPlatform] — portable
                                                         managed reads over IProcFileSystem)
                                IProcessNameResolver.cs (seam + ForCurrentPlatform() + the shared Unnamed(pid)
                                                         wording. Naming a process is NOT portable even
                                                         though looking one up is)
                                WindowsProcessNameResolver.cs
                                                        (Process.GetProcessById + ".exe"; 0 → "System Idle",
                                                         4 → "System". Holds UnsupportedProcessNameResolver.
                                                         [SupportedOSPlatform] ON THE CTOR even though it
                                                         calls no Windows-only API: what it ENCODES is
                                                         Windows-only, and off Windows it would mislabel real
                                                         rows rather than fail. Verified load-bearing —
                                                         removing the factory guard fails the build)
                                LinuxProcessNameResolver.cs
                                                        (over the shared ProcPidName. No .exe and NO
                                                         WELL-KNOWN PIDS — PID 4 is an ordinary kernel thread
                                                         on Linux. A socket with no visible owner shows "—",
                                                         not PID 0, which is not a process)
                                IConnectionsProvider.cs (seam + the ConnectionsSnapshot record)
                                ConnectionsProvider.cs  (TCP+UDP snapshot off the UI thread; PID→name cache
                                                         with stale eviction; de-dupe by key; sort; cap 1000.
                                                         Takes BOTH seams by ctor. IPv6 endpoints are
                                                         BRACKETED — "::1:631" gives no way to tell the port
                                                         from another hextet, and the identity key is built
                                                         from these strings. SINGLE-CONSUMER: the name cache
                                                         is per-instance mutable state)
                                ConnectionInfo.cs       (record + composite identity Key)
                                ConnectionRow.cs        (mutable row VM: only State/StateBrush observable,
                                                         reused across polls via the keyed diff)
                                PingMonitor.cs          (reused in-box Ping; rolling avg/loss + last-3
                                                         lines; soft-fails to a timeout. NO DEFAULT TARGET:
                                                         Target starts EMPTY. It used to default to 8.8.8.8,
                                                         which is how the app came to ping a public resolver
                                                         from launch — and since SetTarget ignores a blank
                                                         value, leaving the constant would let an empty box
                                                         silently resolve back to it)
                                IDnsLookupProvider.cs   (seam + the DnsResult record)
                                DnsLookupProvider.cs    (one-shot Dns.GetHostEntryAsync with a 3 s CTS;
                                                         record type by address family. DefaultHost seeds
                                                         the BOX only — nothing resolves until the user
                                                         presses Look up. No platform prefix — portable)
      /Hardware                 HardwareView.axaml(.cs) + HardwareViewModel.cs
                                                        (spec grid; whole-page scroll like the Dashboard
                                                         — not self-scrolling. VM builds the six fixed
                                                         HardwareCard models, populates them from
                                                         HardwareInfoProvider in the ctor, and implements
                                                         IRefreshablePage; Sensors card left as "—")
                                IHardwareInfoProvider.cs (seam + ForCurrentPlatform(); ONE interface over the
                                                         whole surface — the public shape is already a
                                                         single method returning one aggregate. Holds the
                                                         per-platform reader factories: Windows() carries
                                                         the SupportedOSPlatform because resolving the WMI
                                                         readers is the only Windows-specific step; Linux()
                                                         supplies processor + motherboard and leaves the
                                                         other three cards on Unsupported*)
                                HardwareInfoProvider.cs (async composer: one soft-failing section per card
                                                         → HardwareInfo, the five run concurrently. NO
                                                         platform prefix and NO attribute — the composition
                                                         and its per-card guard are portable, which is what
                                                         keeps them callable from tests on every platform.
                                                         Holds UnsupportedHardwareInfoProvider — .Unknown
                                                         for every card, for a platform with no readers)
                                HardwareInfo.cs         (aggregate snapshot record + per-card sub-records,
                                                         each with .Unknown; fields default to "—")
                                HardwareCard.cs         (observable: fixed title/icon/colours, observable
                                                         Subtitle + ObservableCollection<HardwareSpec> Rows)
                                HardwareSpec.cs         (observable: fixed Key, observable Value → "—")
                                HardwareIcons.cs        (feature-local card glyph geometries + fixed
                                                         per-card icon colours)
                                /Providers              one seam + reader per card, each with its own
                                                         never-throw fall back to that card's .Unknown, so
                                                         one dead source can't blank the others. WmiRead
                                                         holds the WMI boilerplate the Windows readers
                                                         share. Windows* for all five; Linux* for processor,
                                                         motherboard and storage; Unsupported* twins (at the
                                                         bottom of their Windows file) for memory modules
                                                         and graphics.
                                                         LinuxProcessorInfoProvider — the shared CpuFacts
                                                         plus its L3 read; SOCKET IS PERMANENTLY "—"
                                                         (SMBIOS type 4 needs dmidecode as root).
                                                         LinuxMotherboardInfoProvider — DmiIdReader +
                                                         HardwareCatalog, composing the same
                                                         "version (year)" BIOS string as the WMI arm; PCIE
                                                         SLOTS IS PERMANENTLY "—" (SMBIOS type 9 likewise;
                                                         /sys/bus/pci counts occupied devices, not slots).
                                                         LinuxStorageInfoProvider — the shared SysBlockFacts,
                                                         so this card and the Storage tab's cards cannot
                                                         disagree about a drive while keeping their own
                                                         wording ("NVMe" here, "NVMe SSD" there);
                                                         DRIVE HEALTH IS PERMANENTLY "—" (SMART needs root).
                                                         UnsupportedMemoryModulesProvider is permanent too —
                                                         per-DIMM facts are SMBIOS type 17.
                                                         ChipsetNames — the board-name token scan BOTH
                                                         platforms fall back to when the catalog has no
                                                         entry; ordered most-specific-first so B650E does
                                                         not match the B650 token)
                                /Catalog                HardwareCatalog.cs (facade + name normalizer +
                                                         longest-key match) over per-domain static spec
                                                         tables: CpuCatalog / GpuCatalog / BoardCatalog /
                                                         MemoryCatalog (each a spec record + Data dict).
                                                         Fills rated specs WMI can't report; unknown → "—")
      /Performance              PerformanceView.axaml(.cs) + PerformanceViewModel.cs
                                (LIVE — Task-Manager-style master-detail: a 220px resource-selector
                                 rail (ResourceRow item VMs) swaps a right detail pane — one large
                                 Sparkline utilization chart + a 4-tile stat strip (StatTile item VMs).
                                 Fills the viewport via ISelfScrollingPage, like File Explorer. All five
                                 resources (CPU/Memory/Disk/GPU/Ethernet) subscribe to the shared
                                 SystemMetricsService; IRefreshablePage/ILiveSamplingPage/IActivatablePage/
                                 IDisposable.)
                                CpuSpeedFormatter.cs    (Speed tile: the WMI base clock × the PDH clock
                                                         ratio, as GHz; "—" when either is missing)
                                SystemCacheProvider.cs  (page-local psapi GetPerformanceInfo P/Invoke:
                                                         SystemCache pages × PageSize = Task Manager's
                                                         memory "Cached". Soft-fails to null; a thrown
                                                         exception is logged once, then latches it off)
                                MemoryCacheFormatter.cs (Cached tile: bytes → binary GB, "—" when the
                                                         provider reports nothing)
                                IGpuSensorProvider.cs   (GPU Temp/Power tiles. THE TWO PLATFORMS HAVE
                                WindowsGpuSensorProvider.cs OPPOSITE SHAPES. Windows has no in-box sensor
                                IGpuSensorReader.cs      API, so it fans out to one reader per vendor SDK:
                                GpuPciMatcher.cs         NVIDIA temperature via NVAPI (nvapi_QueryInterface
                                GpuSensorFormatter.cs    function-id dispatch, the DXGI vtable technique) and
                                NvApiInterop.cs          power via NVML; AMD temperature via ADL's PMLOG
                                NvmlInterop.cs           snapshot. Those SDKs report NO LUID, which is the
                                NvidiaGpuSensorReader.cs only reason GpuPciMatcher exists — adapters are
                                AdlInterop.cs            joined by PCI identity instead.
                                AmdGpuSensorReader.cs    LinuxGpuSensorProvider needs NEITHER: the kernel
                                PnpPciParser.cs          nests each card's hwmon under the card, so the
                                LinuxGpuSensorProvider.cs adapter key alone finds it and the pci argument is
                                                         unused. temp1_input is MILLIDEGREES and
                                                         power1_average MICROWATTS — two more scales, both
                                                         range-checked, since a wrong divisor still looks
                                                         plausible. No packages, no admin. Every vendor and
                                                         EVERY METRIC soft-fails to "—" independently.
                                                         The vendor readers' EMPTY ANNOTATED CONSTRUCTORS are
                                                         what make the three static interop classes
                                                         unreachable off Windows — see the CA1416 section.
                                                         AMD power + Intel are deferred — see Deferred work)
      /Storage                  StorageView.axaml(.cs) + StorageViewModel.cs
                                (LIVE — read-only drives/health view: a top row of DriveCard summary
                                 cards over a Partitions table (PartitionRow item VMs) + a Disk Activity
                                 card (shared Sparkline, ChartStorage key). Page-scrolls like Network
                                 (not ISelfScrollingPage). Cards from PhysicalDiskProvider/StorageComposer/
                                 VolumeProvider; Disk Activity + Queue from the shared StorageUsageSampler
                                 feed; per-disk Read/Write from IPhysicalDiskThroughputSampler; NVMe Temp
                                 from DiskTemperatureProvider (IOCTL health log). IRefreshablePage/
                                 ILiveSamplingPage/IActivatablePage/IDisposable.)
      /Processes                (the tab itself is described under Feature notes; only its platform seam
                                 is mapped here)
                                IProcessInterop.cs      (seam + ForCurrentPlatform())
                                WindowsProcessInterop.cs
                                                        (kernel32 I/O counters + shell32 Properties sheet.
                                                         Holds UnsupportedProcessInterop. Duplicated from
                                                         File Explorer's shell interop ON PURPOSE — the
                                                         self-contained-tab rule)
                                LinuxProcessInterop.cs  (resolves /proc/[pid]/exe and reveals its folder,
                                                         since no desktop offers a Properties dialog for
                                                         a foreign process. TryGetIoBytes reports nothing
                                                         and that costs the tab NOTHING: the Linux
                                                         snapshot provider does not take this seam and
                                                         reads /proc/[pid]/io itself)
```

Feature-specific *providers* (static WMI/registry reads) live in the tab folder, not `src/Shared`,
until a second feature needs them (per the "keep each tab self-contained" rule). Live **sampling**,
however, is now shared: `SystemMetricsService` owns one sampler per metric and drives it through a
`MetricChannel` at 1 Hz, fanning each sample out to the pages that subscribe (Dashboard, Performance,
Processes). A subscriber keeps its own 60-sample `MetricHistory` (two for network — download + upload)
and rebuilds its `Sparkline` via `history.Points(max)`, using `ChartScale.FitAxis` for the unbounded
network axis. Reuse these seams — do **not** re-inline a per-metric `DispatcherTimer`, declare a bare
`double[]` buffer, or write a bespoke points/peak helper.

**Provider seams.** The OS-touching providers follow the `IGpuSensorReader` shape so they can be faked in
tests and given a second platform later. Most already do; the stragglers are listed under *What CA1416
does not catch* below. The idiom, established by `IStartupRegistration` (`src/Services/Startup`):

- An `internal interface I<Name>` in its own file, **in the folder the provider already lives in** —
  nothing moves, no namespace changes. Its doc comment states the never-throw / soft-fail contract.
- **The `Windows*` / `Unsupported*` split goes exactly where the platform-specific code is, and nowhere
  else.** A genuinely Windows-only reader becomes `internal sealed class Windows<Name>` carrying a
  **class-level** `[SupportedOSPlatform("windows")]` instead of an inner `OperatingSystem.IsWindows()`
  guard, plus an `Unsupported<Name>` **at the bottom of the same file**. `Unsupported*` is **the no-data
  contract for any platform without an implementation** — not "the non-Windows arm". It covers a platform
  whose milestone has not landed yet, and members a supported platform genuinely cannot supply (per-DIMM
  modules needs `dmidecode` with root; disk temperature on Linux). It follows that **if an implementation
  for a new platform would return exactly what `Unsupported*` returns, do not write the class** — leave
  that member on `Unsupported*` in that platform's arm, rather than accumulating empty `Linux*Provider`s. A provider that is **portable managed code keeps its plain name** and gains
  only an interface — naming it `Windows*` when it would run fine anywhere is a lie, and writing an
  `Unsupported*` twin for it would either duplicate the portable body or silently blank a panel that used
  to work. The Network tab is the worked example: `AdapterInfoProvider`, `ConnectionsProvider` and
  `DnsLookupProvider` stay unprefixed, while `IConnectionsInterop` and `IProcessNameResolver` each get the
  full set of arms. **`IProcessNameResolver` is the case worth studying** — its lookup is portable managed
  code (`Process.GetProcessById` runs anywhere), so by the API test it needed no seam at all. It gets one
  because of what it *encodes*: the `.exe` suffix and PIDs 0/4 are Windows facts, and off Windows they
  mislabel real rows rather than failing. **A class can be platform-specific by its knowledge rather than
  by its API surface, and CA1416 cannot see that kind at all.**
- One `ForCurrentPlatform()` picking between them — on the interface for a lone provider, on a bundle
  record (the `MetricSamplers` shape) for a set. That is the **only** place the platform is decided.
- **A view code-behind has no injection point** — `ViewLocator` builds views by name with a parameterless
  ctor. When code-behind needs an interop (it fetches the window handle for the native Properties dialog),
  it calls a small forwarder on the view model it has *already* resolved from `DataContext`, e.g.
  `vm.ShowProperties(handle, pid)`. Do not reach a static from a view.
- Consumers take the interface by constructor. A ViewModel with a parameterless ctor keeps it and chains:
  `public FooViewModel() : this(FooProviders.ForCurrentPlatform()) { }` + an `internal` injecting ctor, so
  `MainWindowViewModel` and `App.axaml.cs` are untouched.
- Everything new is `internal` — which is why `SettingsViewModel`'s ctor is now internal too (a public ctor
  cannot take an internal parameter type). Tests reach it all through `InternalsVisibleTo`.

**Never put `[SupportedOSPlatform]` on the interface** — every consumer would inherit the requirement and
light up CA1416 across the app. Adding a platform later = one new class per interface plus one line in
`ForCurrentPlatform()`; that is the whole point.

**CA1416 is what enforces all of the above.** Both projects build on the neutral `net10.0` TFM with
`TreatWarningsAsErrors`, and neither carries a `NoWarn` for it, so an unguarded call to a Windows-only API
fails the build on both CI legs. Put the attribute on the **narrowest thing that is genuinely
platform-specific**: the type when the whole type is (`WindowsGpuAdapterProvider`), or a single ctor or
method when only that is. The Hardware tab is the worked example — `IHardwareInfoProvider`'s `Windows()`
factory resolves the WMI readers and carries the attribute, while `HardwareInfoProvider`'s composition and
per-card guard are portable, keep no platform in their name, and stay callable from tests on every
platform. (Until M7 the attribute sat on that composer's public ctor, for the same reason; splitting the
factory out is what let a second platform reuse the composition.) Where a guard cannot be seen across a
method boundary, the
attribute restates it: `WindowsSearchIndex.ReadHit` is annotated because its only caller `Run` holds the
`OperatingSystem.IsWindows()` check.

**What CA1416 does not catch.** It fires only on APIs that are themselves annotated — BCL surface like
`System.Management`, OleDb and `Microsoft.Win32.Registry`. **A hand-written `DllImport` declaration carries
no annotation, so a P/Invoke file is invisible to the analyzer.** Annotating those classes is therefore not
busywork: the attribute is the only thing that makes them visible *at their call sites*. Never conclude
from a clean build that the platform surface is covered — grep for `DllImport` and `LibraryImport`.

**Every class that reaches a native Windows API is now covered.** The table that used to sit here is empty;
M13 closed the last three rows, though not in the way it expected — see below.

**A `DllImport` class is covered by the constructor of the type that owns it, not necessarily its own.**
`AdlInterop`, `NvmlInterop` and `NvApiInterop` are `static` classes, so there is no constructor to annotate
and the only options are the type or each member. M13 measured what type-annotating all three costs:
**38 CA1416 errors**, every one inside `AmdGpuSensorReader` and `NvidiaGpuSensorReader`. Two of them are
*const* references (`AdlInterop.MaxSensors`, `NvApiInterop.MaxThermalSensors`) sitting in **field
initialisers** — the one place the rule above says an attribute cannot go. Clearing them would mean
annotating the two reader *types*, which drags their pure decode statics (`SelectTemperature`,
`IsDiscrete`, `SelectGpuSensorIndex`, `PlausibleCelsius`, `PlausibleWatts` — around 30 assertions) behind an
`IsWindows()` guard and costs the Linux leg all of it, for no added protection.

So the attribute goes on the **two reader constructors** instead, which is the real boundary: the interops
are `internal` and reachable only through a reader instance, and a reader can only be constructed inside
`WindowsGpuSensorProvider.CreateReaders()`, which is itself only reachable behind the guard in
`IGpuSensorProvider.ForCurrentPlatform()`. Verified by deleting `CreateReaders()`'s attribute and watching
both reader constructors light up CA1416 individually. The lesson generalises: **when the P/Invoke lives in
a static class, annotate whatever must be constructed to reach it.**

**M13 cleared `GpuUsageSampler`**, the same way M8 cleared its own: two view models held
`private readonly GpuUsageSampler _gpuSampler = new()` and `DeviceInventory` constructed a third inline, so
`IGpuUsageSampler.ForCurrentPlatform()` had to own the construction before the class could be renamed
`WindowsGpuUsageSampler` and annotated. The combined `Sample()` / `SampleEngines()` pair was left off that
seam — no consumer had called either since the multi-GPU split — and then deleted outright once the seam
made it plain they were unreachable.

**M8 cleared the fifth**, and it is the worked example of why the seam has to come first: three view models
held `private readonly PhysicalDiskThroughputSampler _throughputSampler = new()`, and there is nowhere on a
field initialiser to put the attribute. Introducing `IPhysicalDiskThroughputSampler` moved the construction
into one guarded `ForCurrentPlatform()`, and only then could the class be renamed `Windows*` and annotated.
Removing that guard now fails the build — which is the check worth running when adding an attribute, since a
decorative one and a load-bearing one look identical in a passing build.

**The converse case, from M12: a `Windows*` class that must *not* be annotated.** `WindowsToolkitCatalog`
sits behind `IToolkitCatalog.ForCurrentPlatform()` exactly like the samplers do, but it reaches no platform
API at all — it is a table of string literals — so `[SupportedOSPlatform("windows")]` would fail the
delete-the-guard test outright. It also could not be honoured: `Instance` is a `static` field, initialised on
class load *outside* any `IsWindows()` guard, which is the field-initialiser trap M8 hit from the other
direction. **A `Windows*` name is not on its own a reason to annotate** — ask what annotated API the class
touches, and if the answer is none, leave it off and say why in the class doc.

**M5 annotated its four on the *constructor*, not the type, and later milestones should copy that.** A PDH
sampler's `Sample()` and `Dispose()` are guarded by its own `Ready`/`_ready` flag and are genuinely callable
anywhere, as are pure statics like `WindowsLogicalProcessorSampler.TryParseInstance`. Annotating the type
would have dragged all of that behind an `IsWindows()` guard and cost the Linux leg its coverage of the
inert-contract tests and the instance-name parser, for no gain — the attribute's whole job is to make the
*constructor* visible at its call site, which is what forces `CpuUsageSampler`'s ctor and each
`ForCurrentPlatform()` to hold a guard. This is the Hardware tab's shape, applied to samplers.
M6 followed it for both of its classes, including the two whose Windows arm has an **empty** constructor —
an empty annotated ctor is not a wasted one, it is the whole enforcement point, and it kept
`WindowsSystemPerformanceProvider.ToBytes` and the samplers' `SamplerInit.Inert` seam covered on the Linux
leg. M6 also shows what the seam buys beyond the attribute: with `ForCurrentPlatform()` deciding, the inner
`OperatingSystem.IsWindows()` guard inside `Read()` became dead weight and was removed.

**Path hygiene — the other thing CA1416 cannot see.** A path assumption is runtime behaviour, not annotated
API surface, so the analyzer is silent on every one of them: on Unix `\` is an ordinary filename character,
`Path.GetPathRoot(@"C:\x")` is `""`, `Path.IsPathRooted(@"C:\x")` is `false`, `%VAR%` never expands, and
`FileAttributes.Hidden` is a leading dot rather than a bit. M4 fixed the existing ones and left three rules
that keep them from coming back:

- **`Shared/PathComparison` decides whether two strings name the same path** — `OrdinalIgnoreCase` on
  Windows, `Ordinal` elsewhere. Use it for a path-keyed `HashSet`/`Dictionary`, a "did we navigate" check
  or a dedupe. **Do not use it for sorting or filtering names**, which stay `OrdinalIgnoreCase` on every
  platform: those are presentation, and someone typing "doc" expects to find "Documents" whatever the
  filesystem thinks. Getting this backwards silently merges two real folders on Linux.
- **`ToolkitPaths.Resolve` owns environment expansion**, because the notation is per-platform (`%VAR%` on
  Windows; `$VAR`, `${VAR}` and a leading `~` elsewhere). Never call
  `Environment.ExpandEnvironmentVariables` directly — off Windows it is an identity function. Its
  `internal Expand(target, windows)` seam is how both arms stay testable from either dev machine, and
  `IsFileSystemPath(target, windows)` carries the same shape one level up, so the Linux catalog's folder
  rows can be proven reachable by the in-app File Explorer from Windows. **That seam only carries one
  way.** `windows: false` answers correctly on any host, because the Unix notation is expanded here in
  managed code; `windows: true` still needs the variable to *exist*, and `%appdata%` does not on a Linux
  runner. A test asserting the Windows notation keeps its `IsWindows()` guard — passing the flag does not
  remove the need for it, and M12 shipped that mistake briefly before the `OperatingSystem.IsLinux` grep
  caught it.
- **Tests whose subject calls `Path.*` build paths through `Fakes/TestPaths`** (`Root`, `Of(...)`,
  `Dir(...)`), never from drive-letter literals. Where a path is only an opaque token being round-tripped
  — `NavigationHistory`, `RecentSearches` — the literals are clearer and stay. **A forward-slash literal
  is not a safe shortcut either**: `Path.GetDirectoryName` normalises its *result* to the running host's
  separator, so asserting `/home/sophia` fails with `\home\sophia` on a Windows box even when the reader
  is correct. M11 hit exactly that. `Path.GetFileName` does not rewrite anything, so `/`-shaped inputs
  are fine there — the distinction is whether the method returns a path or a segment.

Two traps worth knowing, both found the hard way: a literal reaches `Path.*` **transitively** far more often
than a grep suggests (`ToolkitCommandFactory.ToEntry` → `CanOpenInApp` → `IsPathRooted` was the one that got
through), and `Process.Start` with `UseShellExecute` **succeeds** on Linux where it throws on Windows,
because the target is handed to `xdg-open`. Assert on structure, not on the OS's error text.

`Shared/SystemDrive` carries both shapes of "where the OS lives": `Letter` for matching a Windows volume
record, `Root` for anything that has to open or measure it (`C:\` or `/`, never empty).

**`/proc` access — the Linux counterpart, added in M5.** Every read of `/proc` or `/sys` goes through
`Services/Platform/Linux/IProcFileSystem`, never `System.IO` directly. It exists so Linux providers are
unit-testable from a Windows dev box against canned fixtures (`Fakes/FakeProcFileSystem` +
`Fakes/ProcFixtures`); a provider that opens files itself cannot be tested until someone runs the VM. Three
rules:

- **Build these paths with string concatenation and forward-slash literals — never `Path.Combine`.** On
  Windows `Path.Combine("/proc", "stat")` yields `/proc\stat`, and every fixture lookup then silently
  misses. This is the `/proc` analogue of the drive-letter trap above, and just as invisible to CA1416.
- **`IProcFileSystem` implementations must be stateless and must never throw** — a pseudo-file can vanish,
  change shape or deny access under the reader, and all of that degrades to `null`/empty. Statelessness is
  what lets a provider built on it go into `HardwareProviders`; note that a *sampler* holding a previous
  snapshot (`LinuxCpuSampler`, `LinuxLogicalProcessorSampler`) is stateful and must not.
- **Format knowledge lives in a parser beside the seam, not in a sampler** — `ProcStatParser` is shared by
  the aggregate and per-core CPU samplers, `ProcMeminfoParser` by the memory sampler and the system-counters
  provider, `ProcCpuinfoParser` by `CpuFacts` and the frequency sampler, `OsReleaseParser` and `DmiIdReader`
  by the Dashboard's identity panel and the Hardware tab's Motherboard card, `ProcMountsParser` and
  `ProcDiskstatsParser` by the volume provider and the throughput sampler, and the four per-PID parsers
  (`ProcPidStatParser`, `ProcPidStatusParser`, `ProcPidIoParser`, `ProcCgroupParser`) by the Processes tab's
  walk and its classifier. **One file, one parser, and the file is in the name** — `ProcStatParser` reads
  `/proc/stat` while `ProcPidStatParser` reads `/proc/[pid]/stat`, which are unrelated formats. Parse **by
  index with a length
  check**: the kernel has appended columns to `/proc/stat` over time, so 7-column and 10-column forms both
  have to work, and `/proc/diskstats` grew from 14 fields to 18 in 4.18 and 20 in 5.5. The same defensiveness
  applies to units — `/proc/meminfo`'s `kB` is kibibytes, some of its lines carry no unit at all, sysfs cache
  sizes are suffixed (`8192K`, `16M`) rather than bytes, and both `/sys/block/*/size` and `/proc/diskstats`
  count **512-byte sectors** regardless of the drive's physical sector size.
  Where two cards want the same *derived* numbers rather than the same file, the derivation is shared too:
  `CpuFacts` exists so the Dashboard tile and the Processor card cannot report different core counts,
  `SysBlockFacts` so the Storage tab and the Hardware tab cannot disagree about a drive, and `ProcPids` so
  the Performance tab's process count and the Processes tab's walk cannot disagree about what a process is.

**Not every "not known" can be reported as 0.** The `CpuFacts` convention — report `""`/`0` honestly and let
each consumer place its own placeholder — is right whenever 0 is impossible as a real reading. It is *wrong*
for `/proc/[pid]/status`'s `Uid`, where 0 means root: a denied read reported as 0 silently moves a user's
process into the System group. That field is `int?`, and `LinuxProcessClassifier` has a test pinning that an
unknown owner is not treated as root. Check what 0 means in the domain before reaching for the convention.

**Where a record is keyed by a platform's own identifier, derive an equivalent — do not invent one.** M8's
case: `PhysicalDiskInfo`, `VolumeInfo` and `DiskThroughputSample` are all keyed by an `int` disk number,
which on Windows is the OS's own, so three separately-sampled providers agree for free. Linux names disks
`sda`/`nvme0n1`, and the answer is the kernel's `major:minor` packed as `(major << 20) | minor`
(`SysBlockFacts.Pack`), because it is readable from both `/sys/block/*/dev` and `/proc/diskstats` — so all
three still derive the same key from the same authority. **A positional index would have been the trap**: it
drifts the moment a USB drive is plugged in mid-run, and only between two of the three readers.

**`/proc/mounts` needs two defences a first pass misses.** It lists the same device many times (bind mounts,
`/var/snap`, btrfs subvolumes) and `StorageComposer` *sums* a disk's volumes, so without a dedupe a drive's
capacity and used space multiply. And its device/mount-point fields are octal-escaped, because the separator
is a space.

Three `/proc` gotchas worth knowing: `/proc/stat` lists **online** CPUs only, so per-core state must be keyed
by core number and a core appearing mid-run reports 0 until it has an interval (diffing it against zero
reports its whole since-boot average — this shipped as a bug in M5 and a test caught it); `/proc/cpuinfo`
separates key from value with **tabs**, so parse by trimming around the colon, never by layout; and
`/etc/os-release` is a **shell fragment**, so the same file mixes quoted and bare values.

**Permission-gated sysfs files: do not expose them at all.** `/sys/class/dmi/id/{product_uuid,board_serial,
product_serial}` are mode **0400** — root-only. `DmiIdReader` therefore offers named properties for the
world-readable keys *only*, rather than a general `Value(key)` that would let a caller reach a field which
silently reads `""` for every normal user and logs a denial on every real machine. M7's tests pin this by
asserting `FakeProcFileSystem.Reads` never contains a `*serial` or `*uuid` path — the one degradation that
is invisible in the rendered output.

The **System Information** panel reuses the same async provider pattern on both platforms
(`GetAsync() => Task.Run(Read)`, per-section soft-fail → "Unknown …") reading the static identity facts
once at startup into a `SystemStaticInfo` record; `HardwareProviders.ForCurrentPlatform()` picks the arm.
`WindowsSystemInfoProvider` also reads the **registry** (via the in-box `Microsoft.Win32.Registry` API) for
the build revision (`UBR`) and feature-update label (`DisplayVersion`), which WMI does not expose;
`LinuxSystemInfoProvider` puts the **kernel release** in that same Build row, the closest analogue Linux
has. **Uptime** is the one
live value with no sampler/provider — the VM formats `Environment.TickCount64` (the 64-bit,
non-wrapping tick count) on its own coarse 30 s `DispatcherTimer` (uptime's smallest displayed unit is
minutes). Verbose vendor strings (e.g. "American Megatrends International, LLC.") are shown **in full**;
`InfoRow` wraps them flush-right rather than trimming.

**Network sampler gotcha (important).** `NetworkUsageSampler` samples a **single primary adapter**,
never a sum of all adapters. On .NET, `NetworkInterface.GetAllNetworkInterfaces()` returns many
virtual/filter/phantom adapters (Hyper-V, VirtualBox, WFP, …) that **mirror the physical NIC's byte
counters**, so summing them multi-counts the same traffic (was ~8× too high vs Task Manager). Note a
Windows PowerShell 5.1 probe will **not** reproduce this — .NET Framework returns far fewer adapters
than modern .NET. The sampler selects the internet-facing adapter (Up, non-loopback/tunnel, has a
usable default gateway, busiest by bytes), locks to its `Id` across ticks, and matches Task Manager's
per-adapter numbers. When verifying throughput, always cross-check the actual value against Task
Manager, not just "looks plausible".

**Theming (runtime light/dark + accent).** Colours live in `Palette.axaml` in three groups:
*theme-variant* keys (surfaces, lines, text ramp, hover overlays) sit in
`ResourceDictionary.ThemeDictionaries` under `Dark`/`Light` and flip with the app's `ThemeVariant`;
the *accent set* (`Accent`, `AccentHover`, `OnAccent`, `AccentSoft`, `AccentColor`/`AccentDeep`) and the
per-graph *chart-series* keys (`ChartCpu`, `ChartMemory`, `ChartGpu`, `ChartStorage`, `ChartNetDown`,
`ChartNetUp`) sit top-level and are **swapped at runtime**. **Rule:** any key that can change at runtime
must be referenced with `{DynamicResource ...}`, never `{StaticResource}` (only the fixed legend colours
`Blue`/`Green`/`Purple`/`Orange`/`Yellow` stay static). `ThemeService` (`src/Services/Theming`) is the
**only** code that writes to `Application.Current` — `ApplyTheme` sets the variant; `ApplyAccent` swaps
the accent and installs the palette derived from it; `ApplyDefaultAppearance` restores the authored one.
It's constructed once in `MainWindowViewModel`, applied at startup, and handed to `SettingsViewModel` and
`PerformanceViewModel`. Note this feature deliberately touched shared styles + the shell
(Palette/SharedStyles, MainWindow, NavItem) — theming is cross-cutting, so it lives in `src/Services`,
not a tab.

**An accent re-hues the graphs; it must never flatten them.** Selecting an accent used to set all six
chart-series keys to that one colour, which erased the per-metric coding the charts depend on — worst on
the Dashboard's Network Throughput chart, where download and upload share an axis and became one
indistinguishable line. `ChartPalette.Derive` now rotates the whole authored palette instead: the accent
becomes the CPU (and net-down) series, and every other series keeps its own saturation and lightness while
its hue turns by the accent's offset from the default blue. The authored spacing between hues therefore
survives whatever accent is chosen, and `Derive(AccentPreset.Default.Color)` reproduces `ChartPalette
.Default` exactly, so the blue swatch and the "Default" swatch agree. **`ChartPalette` is the single
source of these colours** — do not parse a series hex anywhere else. The Performance tab used to, giving
the app two contradictory answers to "what colour is CPU"; its `ResourceRow`s now carry a `ChartSeries`
identity and resolve the brush through `ThemeService.BrushFor`, re-applying on `SeriesChanged`. Status
colours are a different thing and stay fixed: Storage's health pills, its usage bars and File Explorer's
type glyphs must not follow an accent — "Healthy" is green whatever the user picked.

**Charting conventions.** Every live chart is the shared `Sparkline` on a fixed 0–100 axis, drawn from a
`MetricHistory` via `history.Points(max)`, with a gradient `Fill` and a `ShowGrid` lattice. The grid is a
**scale, not decoration** — its four bands are quarters of the axis, and the middle line is the value the
panel charts label "50%" — so do not give one chart its own row count; a card ruled into halves marks
different values than the chart beside it. Axis furniture is graded by how much room a chart has:

- **A chart in a panel of its own** — `chartPanel`, `chartHero`, and the Network tab's `chartMini`
  throughput traces — carries the lot: a caption of what it plots and over how long, three value labels,
  the time range, and a `StatusText`. It is the enclosing panel that decides this, not the size class.
- **Stat cards** (`chartMini` inside `StatCard`) carry the axis **ends only** and no time range — three
  labels would touch at a card's height and a footer would take most of the plot, and the cards share the
  window the panel charts below them state. Their ceiling is per-card (`DashboardCard.AxisMaxLabel`): most
  plot a percentage, but the network card fills to its own live peak, so a blanket "100%" lies on it.
- **Per-core / per-engine cells** (`chartCell`) stay bare. They tile many-to-a-pane, and a gutter on each
  would cost more than the labels are worth at that size.

Never hardcode a window in a caption: it is the refresh interval times the slot count, so say it through
`ChartWindow.Describe` / `StartLabel`. The oldest end reads as elapsed time ("60s ago"), never as a
negative offset — a leading minus suggests a value below zero on a chart whose y axis starts there.

Namespaces follow folders: `DashDetective.Shared`, `DashDetective.Shared.Controls`,
`DashDetective.Services.Theming`, `DashDetective.Shell`, `DashDetective.Shell.Navigation`,
`DashDetective.Tabs.<Feature>`.
The `ViewLocator` maps a `*ViewModel` to its `*View` by name, so a tab's View and ViewModel
must share a namespace.

Rules of thumb:
- Anything reused by more than one tab (styling, colours, widgets) belongs in `src/Shared`.
- Keep each tab self-contained: its view, view model, and feature-specific helpers live in
  its own folder under `src/Tabs`, not scattered project-wide.
- The shell (sidebar/toolbar/navigation) is shared — edit carefully.
- **Reuse the shared abstractions instead of re-inlining the old patterns:** `MetricChannel` +
  `SystemMetricsService` (live sampling), `MetricHistory` + `ChartScale` + `ChartAxis` +
  `ChartWindow` + `ChartStatus` (charts), `ChartPalette` (series colours),
  `CollectionReconciler` (keyed-diff live lists), `HardwareNameFormatter` (CPU/GPU name trim),
  `UptimeFormatter` / `DataRateFormatter` (formatting), and `Log` (diagnostics behind soft-fail catches).

## Dependencies

Beyond Avalonia + `CommunityToolkit.Mvvm`, the project references **`System.Management`**
(added for the live-CPU work, with user approval) — it provides WMI access (`Win32_Processor`,
`Win32_PhysicalMemory`, etc.). Reuse it for future hardware queries. The live-Network work (Dashboard
throughput **and** the full Network tab) added **no** new package — it uses the in-box
`System.Net.NetworkInformation` (throughput + adapters/IP), `System.Net.NetworkInformation.Ping`,
`System.Net.Dns`, and `iphlpapi` P/Invoke for the connections table (feature-local `ConnectionsInterop`,
like File Explorer's `ShellInterop`). Adding any *new* package still requires asking first (see Strict
Working Boundaries).

The **GPU sensor** work (Performance Temp/Power tiles) also added **no** new package. It P/Invokes three
DLLs that the *display driver* installs into `System32` — `nvapi64.dll`, `nvml.dll` and `atiadlxx.dll` —
so there is nothing to reference, redistribute or ship, and no admin rights are involved. A unified sensor
library (LibreHardwareMonitorLib and similar) was **deliberately ruled out** for this project; do not
propose one. If a machine lacks a vendor's driver the `DllImport` simply fails and the tile stays "—".

The System Information work reads the **registry** via the `Microsoft.Win32.Registry` API (build
revision + feature-update label). On the `net10.0` target this API is **provided in-box — no package
reference is needed** (adding the `Microsoft.Win32.Registry` package is redundant and raises an
`NU1510` "unnecessary" warning). So it, too, added **no** new dependency.

The **Settings persistence** work (settings store + "Launch at startup" + system tray) likewise added
**no** new package: `System.Text.Json` (source-generated `SettingsJsonContext`) and
`Microsoft.Win32.Registry` (the HKCU `Run` key) are in-box on `net10.0`, and Avalonia's
`TrayIcon` ships with the framework. Reuse the in-box JSON + registry for future persisted state.

## Testing conventions

Unit tests live in **`tests/DashDetective.Tests`** (xUnit, `net10.0`, referenced by `DashDetective.sln`).
CI builds, format-checks, tests **and** collects coverage on `windows-latest` and `ubuntu-latest`, in
Debug and Release — four legs, none of them guarded. `dotnet format` gates the test code on both, so keep
usings alphabetical (`System` is **not** sorted first).

**A green Windows run proves nothing about the Linux leg.** Reader-identity tests branch on the host, so
the arm asserting Linux never executes locally. Before calling any work that touches a
`ForCurrentPlatform()` green, grep the test project for **both**:

- `OperatingSystem.IsLinux` — the tests that already know about three platforms.
- `IsType<Unsupported` — **the one that actually catches it.** A two-arm test reads
  `if (IsWindows()) … else Assert.IsType<Unsupported…>` and contains no `IsLinux` at all, so the first
  grep walks straight past it. M11 added a Linux arm to `IShellInterop` and only this second grep found
  `ShellInteropTests`, which would have failed the Ubuntu leg.

To prove a fix without CI, invoke the private factory by reflection from a throwaway test
(`typeof(HardwareProviders).GetMethod("Linux", BindingFlags.Static | BindingFlags.NonPublic)`), assert the
types on Windows, then delete it.

- **Layout mirrors the app.** A test file sits under the same relative path as its subject
  (`src/Shared/Charts/SparklinePoints.cs` → `tests/DashDetective.Tests/Shared/Charts/SparklinePointsTests.cs`),
  one `*Tests` class per production type, in a matching `DashDetective.Tests.*` namespace.
- **Naming + voice.** Test methods read `Method_Scenario_Expectation`
  (e.g. `Build_NonPositiveMax_PinsEveryPointFlat`); XML-doc each test class with the contract it pins,
  mirroring this repo's documentation voice.
- **Hand-rolled fakes, no mocking framework.** Fakes are small hand-written classes under
  `tests/DashDetective.Tests/Fakes` (e.g. `FakeUiTimer`) — matching the codebase's zero-dependency ethos.
- **Test seams are minimal and behaviour-preserving.** To keep logic testable headlessly, a few
  `internal` seams are exposed via `InternalsVisibleTo("DashDetective.Tests")` (in the app csproj), never
  a behaviour change:
  - `IUiTimer` + `DispatcherTimerAdapter` (`src/Services/Threading`) — a UI-thread-timer seam so
    `MetricChannel` / `SystemMetricsService` can be driven without an Avalonia dispatcher; production
    still uses a real `DispatcherTimer` by default.
  - `IProcFileSystem` + `ProcFileSystem` (`src/Services/Platform/Linux`) — the same shape for `/proc` and
    `/sys`, so every Linux provider is testable from a Windows box against `FakeProcFileSystem` fixtures.
    Each Linux sampler and provider takes it by ctor, with a parameterless chain for production.
  - `SystemMetricsService`'s `internal` ctor takes a `MetricSamplers` bundle + a timer factory, so the
    five hardware samplers can be faked.
  - `SettingsStore`'s `internal` ctor takes an explicit file path (production resolves `%AppData%`).
  - `private → internal` widenings (`HardwareCatalog.Match`, `CurrentUserProvider.DeriveInitials`) and
    one behaviour-preserving extraction (`NetworkViewModel`'s pager math → `PagerMath`).
- **Every new `src/Shared` or `src/Services` type ships with tests.** Pure logic (formatters, catalogs,
  chart/paging math) is tested directly; timer/sampler-driven types are tested through their seam with
  fakes plus the synchronous entry points (`SampleNow` / `RefreshAll` / `Flush`), not by waiting on a
  real timer.
- **No render backend in tests.** These are plain xUnit tests with no Avalonia app, so anything reaching
  `Geometry.Parse` throws `Unable to locate 'IPlatformRenderInterface'`. Because that runs in `Icons`'s
  static initialiser, touching **any** `Icons` member — even a pure static method — fails. Keep glyph
  *selection* rules out of `Icons` (as `NavigationViewModel.ChevronPointing` → `ChevronDirection` does)
  and leave `Icons` a plain geometry lookup, so the rule stays testable and the geometry never loads.

## Working Style

- One detail at a time. Prefer small, focused changes over broad sweeps.
- When a feature folder doesn't exist yet but is in Current Scope, it's fine to create it.
- Match existing conventions (naming, MVVM patterns, styling) already established in the
  Dashboard/Settings/default window code rather than introducing new patterns.
- If you're unsure whether something is in scope, ask rather than assume.

## Updating This Document

When a new feature becomes active, or an existing one is completed/paused, update the
**Current Scope** section above to reflect it. This file should always describe what is
*actually* being worked on right now — not the full long-term plan.

## Appendix — Completed Feature Details

> These are the full write-ups for features that are already live/complete. They were moved out of
> **Current Scope** to keep the working section scannable; nothing here is out of date — it is the
> detailed reference behind the condensed bullets above.

- **Navigation bar (shell-level).** The sidebar is a self-contained, **collapsible and dockable**
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

  Orientation/collapse and every derived layout value (dock edge, rail thickness, item axis,
  label/brand/footer visibility, accent-indicator bar↔underline, scroll axis, the puck's size /
  alignment / rounding) are **computed properties on the VM — no value converters**. The rail
  thickness has a **single owner**, `RailThickness(horizontal)`, which `RailWidth`/`RailHeight` delegate
  to and the drop preview measures against; it takes the axis as an argument because a drag previews
  edges the bar is not docked to yet. `MainWindowViewModel` owns page routing and delegates the bar to
  `Nav`, wiring `Nav.SelectionChanged` → `CurrentPage`. Orientation and collapse **persist** (see
  *Persistence* below); this is shared shell work, not a tab-local change.

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

- **Dashboard** — the **CPU, Memory, GPU, Storage and Network surfaces are live and functional**. CPU:
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

- **Universal search** — **fully live** (plan:
  `C:\Users\User\.claude\plans\make-a-plan-to-lexical-salamander.md`). The toolbar box (`Ctrl+F`) searches
  six categories at once and navigates to whatever is picked, revealing it in place.
  - **Structure** (`src/Shell/Search/`). `SearchRanker` scores a term against text in four tiers kept
    200 apart (exact / prefix / word-start / anywhere) with a closeness bonus capped below 100, so a
    tier can never be crossed. `SearchAggregator` fans one query out to independent `ISearchProvider`s,
    merges and caps what comes back, and discards an answer whose term the user has already typed past.
    A provider that throws costs its own category and nothing else.
  - **Providers.** Pages (over the live nav items), Settings (over `SettingCatalog`), Shortcuts (over
    `ShortcutCatalog.HelpGroups`, so a result already knows its scope and keys), Toolkit (over
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

- **Settings** — **fully live** (plan: `C:\Users\User\.claude\plans\you-are-working-in-silly-planet.md`).
  - **Appearance.** The **Theme** segmented control (Dark / Light / System) and the **Accent color**
    swatches are data-bound to `SettingsViewModel` and applied at runtime through a single
    `ThemeService` (see *Theming* below). The accent row's **first** swatch is a "Default"
    (multi-colour) option — a 2×2 four-colour square that restores the authored look (each dashboard
    graph its own colour, highlight blue); the four single-colour swatches recolour the highlight and
    hand the graphs a palette **derived** from that accent, each metric keeping a hue of its own.
  - **Monitoring.** The **Refresh interval** segments (0.5 / 1 / 2 / 5 s) are real `IntervalOption`
    selectable-item VMs (the `ThemeOption` pattern); selecting one calls
    `SystemMetricsService.SetInterval`, which retimes **only** the five 1 Hz metric channels — the
    coarse timers stay coarse (Dashboard uptime 30 s; Network adapters 5 s / connections 2.5 s /
    ping 2 s are NOT retimed). The three toggles are real templated `ToggleButton`s (shared
    `ToggleButton.toggle` style in `SharedStyles.axaml`, pixel-matching the old mock): **Resource
    alerts** (merged from the comp's two notification toggles — no OS toast is in scope, so both meant
    the same in-app banner), **Show in system tray**, **Launch at startup**. The alert watcher lives in
    `SystemMetricsService` (raises `AlertActiveChanged` after CPU or memory stays ≥ 90 % for 10
    consecutive samples); the shell shows an inline warning banner below the toolbar (auto-clears on
    recovery, `×` to dismiss the current breach, gated by the setting). **Launch at startup** writes the
    HKCU `…\Run` value via `IStartupRegistration` (`src/Services/Startup`, soft-failing).
  - **System tray.** A `TrayIcon` declared in `App.axaml` (Show / Exit menu, wired in `App.axaml.cs`);
    with the setting on, closing the window hides to tray (`MainWindow.OnClosing`) instead of exiting.
    Real exit still runs the composition root's disposal.
  - **Export & Data.** Handlers in `SettingsView.axaml.cs` (own the save dialog + clipboard, needing
    the `TopLevel`, like `MainWindow`): **Copy diagnostics** → clipboard; **Export report (.txt)** →
    the same plain-text report as the toolbar Export (no PDF library); **Export CSV** → the rolling
    60-sample metric histories (`DashboardViewModel.BuildMetricsCsv`). `MainWindowViewModel.BuildReport`
    now appends a Hardware summary and the primary network config (via small read-only accessors —
    `HardwareViewModel.GetReportRows`, `NetworkViewModel.GetPrimaryConfigRows`).
  - **Persistence.** All of the above (plus Appearance and Navigation) persist to
    `%AppData%/DashDetective/settings.json` via `SettingsStore` (`src/Services/Settings`; System.Text.Json
    source-gen, load-on-start with full soft-fail to defaults, debounced atomic save, `schemaVersion`).
    The composition root (`App` → `MainWindowViewModel`) applies a loaded snapshot through the seams and
    observes them to save; `ThemeService` stays the single theming applier — the store only observes.
    `TrayNoticeShown` rides along but is **not a preference** and has no Settings row: it is the record
    that the app has disclosed, once, that closing the window does not stop it.
    Theme, accent and the navigation choices **persist** through this rather than lasting a session.

- **File Explorer** — **live and functional** (built in phases; plan:
  `C:\Users\User\.claude\plans\create-a-detailed-plan-jolly-bonbon.md`). A **read-only** three-pane
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

- **Network** — **live and functional** (built in phases; plan:
  `C:\Users\User\.claude\plans\plan-and-brainstorm-how-iterative-wave.md`). Matches the design comp's
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

- **Processes** — **live and functional** (built in phases; plan:
  `C:\Users\User\.claude\plans\processes-tab-plan.md`). A Task-Manager-style live process view: the
  list **split three ways — Apps / Background processes / Windows processes** (per `ProcessClassifier`
  + `ProcessCategory`), per-process **PID / status / CPU % / Memory / Disk / GPU %**, **sortable
  column headers**, a summary strip (**process counts per group**, **total CPU %**, **total
  Memory %**, **total thread count**), **End task** (behind a confirmation overlay — killing a
  process is destructive), and native **Properties** (the exe's shell property sheet), both acting on
  the selected row. Multi-process apps **collapse into a single entry** with aggregate metrics,
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
  7 columns. Follows the shared page-lifecycle pattern (constructed once in the shell; `IRefreshablePage` +
  `ILiveSamplingPage` + `IActivatablePage` + `IDisposable` + `ISelfScrollingPage`), the Network tab's keyed-diff live table
  (via the shared `CollectionReconciler`, so rows are reused and the list doesn't flicker), and the
  File Explorer sortable-header + Properties patterns. The list polls on its own 2 s timer
  (enumerating every process is heavier than a single counter); the summary strip's system-wide
  CPU %/Memory % come from the shared `SystemMetricsService`.

- **Performance** — **live and functional** (built in phases; plan:
  `C:\Users\User\.claude\plans\develop-a-plan-to-elegant-thimble.md`). A Task-Manager-style resource
  drill-down per the design comp: a left **resource-selector** rail (CPU · Memory · Disk 0 (C:) · GPU ·
  Ethernet) of `ResourceRow` item VMs swaps a right **detail pane** — one large utilization chart
  (reuses the shared `Sparkline`, fixed 0–100 axis + gradient fill + background grid) plus a 4-tile stat
  strip (`StatTile` item VMs). Self-contained tab under `src/Tabs/Performance/` (`PerformanceView` +
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
  `SystemMetricsService` were untouched. The Memory **Cached** tile is live too: the page-local
  `SystemCacheProvider` calls the in-box psapi `GetPerformanceInfo` and scales its
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

  **GPU Temp and Power are live too** (2026-07, plan:
  `C:\Users\User\.claude\plans\you-re-working-in-the-melodic-star.md`). This **supersedes the old claim that
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

### Completed cross-cutting passes

> Two one-off passes outside the usual per-feature boundaries, each authorised under explicit sign-off.
> Recorded here for history — completing them did **not** widen the working boundaries; any further
> cross-cutting or out-of-feature change still needs its own explicit sign-off.

- **Repo-hygiene / portfolio pass — COMPLETED (2026-07-18).** A portfolio `README.md`, a reader-facing
  `docs/ARCHITECTURE.md` (distilled from this appendix), project metadata in the csproj (`Version 0.1.0`,
  title/authors/copyright, retarget to `net10.0-windows`), analyzer + warning gates (`AnalysisLevel
  latest`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`) with a root `.editorconfig` encoding the
  existing style, a `dotnet format --verify-no-changes` step in CI, and the Settings footer wired to a
  real assembly version via `AppInfo` (`src/Shared`) instead of the old fictional string. This did
  **not** change any feature behaviour (the footer text is the sole exception).

- **De-duplication / composition refactor — COMPLETED (2026-07-19).** A cross-cutting pass over
  `src/Shared`, `src/Services`, `src/Shell` and the Dashboard / Performance / Network / Processes tabs,
  with **zero user-visible behaviour change**. It replaced the ~10× copy-pasted per-metric
  `DispatcherTimer` + rolling-buffer pattern with `MetricChannel` + a shared `SystemMetricsService` (one
  sampler set, ref-counted subscriptions, removing the duplicate PDH GPU/disk queries); consolidated the
  chart/format/diff duplication into `MetricHistory`, `SparklinePoints`, `ChartScale`,
  `HardwareNameFormatter` and
  `CollectionReconciler`; added real shutdown disposal via a manual composition root in `App`; switched
  `NavigationView`/`MainWindow` fan-out to `[NotifyPropertyChangedFor]`; replaced the reflection
  `ViewLocator` with a compile-time switch; and added the soft-failing `Log` seam.