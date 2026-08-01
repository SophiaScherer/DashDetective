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

**Already-live features — read for consistency (shared styles, naming, the always-on / self-scrolling
patterns)** (full write-ups in *Appendix — Completed Feature Details*): the shell **Navigation bar**,
**Dashboard**, **Settings** (fully live — Appearance, Navigation, Monitoring and Export & Data),
**File Explorer**, **Network**, **Processes**, **Performance**, **Hardware**, **Storage** (live —
drives/health view; status below), **Toolkit** (in progress; status below) and **Keyboard shortcuts**
(status below). Two cross-cutting passes are also complete (repo-hygiene / portfolio pass;
de-duplication / composition refactor) — write-ups in the Appendix.

**Toolkit — implementation status** (IN PROGRESS — execution is live; the catalog is being authored):

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
  stanza to the Execution Log. **`ToolkitCatalog.Entries` is the app's allow-list**: the runner only ever
  runs an action authored there, arguments reach the OS through `ProcessStartInfo.ArgumentList` rather
  than a joined command line, and there is **no free-form command box anywhere — do not add one**.
  Elevation is its own `ToolkitActionKind` (not a flag) because Windows refuses to redirect a `runas`
  process's streams, so "elevated *and* captured" is not expressible.
- **Catalog progress:** all four categories authored — Folders, System Tools, Diagnostics (parameterised
  ping/tracert and the elevated `sfc /scannow`) and Docs & Links. Still to come: the per-row
  copy-to-clipboard, pinned favourites and log export. Take these on **one phase at a time** per the plan
  above — running commands is a **security-relevant** capability, not a follow-on tidy.
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
  `ToolkitDefaults` (reusing `NetworkUsageSampler.SelectPrimary`), off the UI thread and **never over a
  value already typed**. The log's `$` line shows `ToolkitAction.CommandLine` — the resolved target plus
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
- The per-row copy button is still **placed but inert**; Clear really does empty the log and is simply
  disabled while it is empty.
- Self-scrolling (`ISelfScrollingPage`) so the log panel stays pinned while the list scrolls; the
  comp's `position:sticky` has no Avalonia equivalent. Wired to `IShortcutTarget` (`ShortcutScope.Toolkit`,
  `/` focuses the filter, `Esc` clears it) and to universal search via `ToolkitSearchProvider` — see the
  reveal gotcha in the *Universal search* write-up in the Appendix. Not `IRefreshablePage` /
  `ILiveSamplingPage`: the page has nothing live to sample or re-read.
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
  `Sparkline` (with the `ChartStorage` amber key), and the built-in `ProgressBar` for the usage bars.
  Live sources: the drive cards from `PhysicalDiskProvider` + `StorageComposer` + `VolumeProvider`; the
  Disk Activity chart + Active time / Avg response / **Queue** readouts from the shared `StorageUsageSampler`
  feed (via `SystemMetricsService`); per-disk **Read/Write** from the page-local
  `PhysicalDiskThroughputSampler` (its own 1 Hz timer, deliberately not retimed by Settings); and each NVMe
  card's **Temp** from `DiskTemperatureProvider` (non-admin `IOCTL_STORAGE_QUERY_PROPERTY` health-log read,
  refreshed on a slow ~15 s sub-cadence of the throughput timer). Wired to `IRefreshablePage` /
  `ILiveSamplingPage`. Non-NVMe drives show "—" for Temp; SATA/HDD/USB drive temperature stays deferred
  (needs admin or vendor SDKs). No new packages, no new shared controls. (**GPU** temperature is no longer
  deferred — it is live on the Performance tab via per-vendor SDKs; see the write-up in the Appendix.)

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
- `Win32_VideoController` (`GpuInfoProvider`) is retained only for the single-name callers; the inventory
  uses `GpuAdapterProvider`.

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
                               MainWindowViewModel.ToggleLive routes SetLive() over every nav page —
                               Dashboard + Network)
      HardwareNameFormatter.cs (static: trims vendor/marketing decoration from CPU/GPU names for the
                                compact captions; shared by Dashboard + Performance. Distinct from
                                HardwareCatalog.Normalize — display trim, not a lookup key)
      CollectionReconciler.cs  (generic keyed diff of an ordered snapshot into an ObservableCollection —
                                drop/update/move/insert in place, no flicker; shared by the Network
                                connections table + the Processes list)
      /Charts
        SparklinePoints.cs      (renders a rolling metric history to a Sparkline "x,y" points string on a
                                 fixed 0–100 axis; percentage metrics pass valueMax 100, unbounded ones a
                                 rolling peak. Used by every live chart)
        ChartScale.cs           (peak/headroom/floor auto-scaling for the network throughput axis —
                                 Peak / FitPeak / FitAxis; shared by Dashboard + Performance + Network)
      /Styles
        Palette.axaml           (colour brushes; merged in App.axaml. Light/Dark live in
                                 ResourceDictionary.ThemeDictionaries; accent + chart-series keys
                                 sit top-level and are swapped at runtime — see Theming below)
        SharedStyles.axaml      (reusable class styles: card, panel, seg, toggle, buttons,
                                 paneSplitter (draggable divider between resizable panes)…)
      /Controls
        Sparkline, StatCard, InfoRow   (reusable widgets; Sparkline auto-fits to its data
                                        by default, or set YMin/YMax for a fixed axis —
                                        StatCard forwards YMin/YMax to its inner sparkline.
                                        Fixed-axis mode also supports an optional second series
                                        (Points2/Stroke2) + gradient area fill (Fill), used by the
                                        Network throughput panel for download+upload on one scale.
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
        StartupRegistration.cs  (HKCU …\Run add/remove for "Launch at startup"; Microsoft.Win32.Registry,
                                 Windows-guarded + soft-failing, like CurrentUserProvider)
      /Diagnostics
        Log.cs                  (minimal soft-failing logger → Debug output + a per-day rolling file in
                                 %LocalAppData%/DashDetective/logs; never throws. The sampler / provider /
                                 MetricChannel catch blocks route through Log.Warn, and Program.cs hooks
                                 AppDomain.UnhandledException + TaskScheduler.UnobservedTaskException →
                                 Log.Error. No logging packages)
      /Theming
        ThemeService.cs         (single seam that applies theme + accent to Application at runtime)
        AppTheme.cs             (enum: System / Light / Dark)
        AccentPreset.cs         (record: one accent's Color/Hover/OnAccent/Deep; .All = the four)
      /SystemMetrics
        ProcessorFrequencySampler.cs (live CPU current-clock ratio via PDH
                                 \Processor Information(_Total)\% Processor Performance; page-local to the
                                 Performance tab's CPU Speed tile — × the WMI base clock, like Task Manager.
                                 NOT the % Processor Utility counter, which is utilisation, not clock speed)
        CpuUsageSampler.cs      (live total CPU % via GetSystemTimes)
        MemoryUsageSampler.cs   (live RAM % + used/total via GlobalMemoryStatusEx)
        GpuUsageSampler.cs      (live GPU % via PDH GPU Engine counters; owns a PDH query handle. Sample()
                                 = busiest engine overall, SampleEngines() = per-engine map, SampleAdapters()
                                 = per-physical-GPU split keyed by adapter LUID token. Page-local per tab —
                                 the Dashboard cards + Performance rows each own one for per-adapter readings)
        GpuAdapterProvider.cs   (DXGI adapter enumeration via raw vtable fn-pointers — LUID→name + software
                                 flag + VRAM; the authoritative LUID→name map for multi-GPU, async. Its
                                 DedicatedVideoMemory rides through DeviceInventory onto
                                 DeviceInstance.VramBytes → the Performance GPU VRAM tile)
        StorageUsageSampler.cs  (live disk Active time % + read/write/response via PDH PhysicalDisk
                                 counters; owns a PDH query handle)
        DiskInfoProvider.cs     (static primary-disk model/type/capacity via WMI, async)
        MetricChannel.cs        (reusable "sampler + DispatcherTimer + rolling double[window] history"
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
                                 the App composition root and disposed on shutdown.)
      /Network
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
                                CpuInfoProvider.cs      (static CPU info via WMI, async)
                                CpuStaticInfo.cs        (record for the WMI result)
                                MemoryInfoProvider.cs   (static RAM info via WMI, async)
                                MemoryStaticInfo.cs     (record for the WMI result)
                                GpuInfoProvider.cs      (static GPU name via WMI, async)
                                GpuStaticInfo.cs        (record for the WMI result)
                                (the CPU/Memory/GPU/Storage/Network *samplers* now live under
                                 src/Services/SystemMetrics + /Network and are owned by
                                 SystemMetricsService — the Dashboard VM subscribes, it no longer owns them)
                                SystemInfoProvider.cs   (static system identity — OS/device/BIOS/board/build —
                                                         via WMI + registry, async; uptime is live off
                                                         Environment.TickCount64 in the VM, no sampler file)
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
                                ToolkitCatalog.cs       (static copy table + the command set. Entries is
                                                         also the app's ALLOW-LIST — the runner only ever
                                                         runs an action authored here)
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
                                                         deadlocks and a killed one loses what it printed)
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
      /FileExplorer             FileExplorerView.axaml(.cs) + FileExplorerViewModel.cs
                                                        (VM implements ISelfScrollingPage +
                                                         IRefreshablePage; owns filter, sort + ShowHidden
                                                         state and RebuildVisibleEntries; drives live
                                                         auto-refresh + scroll-to-top-on-navigation)
                                DirectoryService.cs     (async System.IO enumeration: drives, lazy
                                                         subdirectories, folder entries; per-entry
                                                         soft-fail, Task.Run off the UI thread; takes
                                                         includeHidden to reveal hidden/system entries.
                                                         FileItem carries raw Size/Modified sort keys)
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
                                FileTypeCatalog.cs      (extension → vector glyph + fixed colour)
                                ShellInterop.cs         (feature-local shell32 P/Invoke:
                                                         SHGetFileInfo type name + SHObjectProperties)
      /Network                  NetworkView.axaml(.cs) + NetworkViewModel.cs
                                                        (VM implements IRefreshablePage + ILiveSamplingPage;
                                                         always-on like Dashboard. Owns the throughput
                                                         sampler + adapter/connection/ping/DNS timers and
                                                         the keyed-diff for the connections list. Tab-local
                                                         MonoFont + fixed console-colour resources live in
                                                         the view — promote to Shared if reused)
                                AdapterInfoProvider.cs  (async snapshot: all adapters + primary IP config
                                                         via managed NetworkInterface; SystemInfoProvider
                                                         pattern, per-adapter/field soft-fail)
                                AdapterInfo.cs          (record + AdapterKind enum; fixed status-dot brushes)
                                IpConfigInfo.cs         (record: IPv4/mask/gateway/DNS/MAC/DHCP; .Unknown)
                                ConnectionsInterop.cs   (feature-local iphlpapi P/Invoke:
                                                         GetExtendedTcpTable/GetExtendedUdpTable, IPv4
                                                         OWNER_PID tables; port byte-order swap. IPv6 deferred)
                                ConnectionsProvider.cs  (TCP+UDP snapshot off the UI thread; PID→name cache
                                                         with stale eviction; de-dupe by key; sort; cap 100)
                                ConnectionInfo.cs       (record + composite identity Key)
                                ConnectionRow.cs        (mutable row VM: only State/StateBrush observable,
                                                         reused across polls via the keyed diff)
                                PingMonitor.cs          (reused in-box Ping to 8.8.8.8; rolling avg/loss +
                                                         last-3 lines; soft-fails to a timeout)
                                DnsLookupProvider.cs    (one-shot Dns.GetHostEntryAsync to example.com with a
                                                         3 s CTS; record type by address family)
      /Hardware                 HardwareView.axaml(.cs) + HardwareViewModel.cs
                                                        (spec grid; whole-page scroll like the Dashboard
                                                         — not self-scrolling. VM builds the six fixed
                                                         HardwareCard models, populates them from
                                                         HardwareInfoProvider in the ctor, and implements
                                                         IRefreshablePage; Sensors card left as "—")
                                HardwareInfoProvider.cs (async WMI reader, SystemInfoProvider idiom: one
                                                         soft-failing section per card → HardwareInfo)
                                HardwareInfo.cs         (aggregate snapshot record + per-card sub-records,
                                                         each with .Unknown; fields default to "—")
                                HardwareCard.cs         (observable: fixed title/icon/colours, observable
                                                         Subtitle + ObservableCollection<HardwareSpec> Rows)
                                HardwareSpec.cs         (observable: fixed Key, observable Value → "—")
                                HardwareIcons.cs        (feature-local card glyph geometries + fixed
                                                         per-card icon colours)
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
                                 SystemMetricsService; IRefreshablePage/ILiveSamplingPage/IDisposable.)
                                CpuSpeedFormatter.cs    (Speed tile: the WMI base clock × the PDH clock
                                                         ratio, as GHz; "—" when either is missing)
                                SystemCacheProvider.cs  (page-local psapi GetPerformanceInfo P/Invoke:
                                                         SystemCache pages × PageSize = Task Manager's
                                                         memory "Cached". Soft-fails to null; a thrown
                                                         exception is logged once, then latches it off)
                                MemoryCacheFormatter.cs (Cached tile: bytes → binary GB, "—" when the
                                                         provider reports nothing)
                                IGpuSensorReader.cs     (GPU Temp/Power tiles — one swappable reader per GPU
                                GpuSensorProvider.cs     vendor behind a common interface, plus the routing
                                GpuPciMatcher.cs         + pure join/format helpers. Windows has no in-box GPU
                                GpuSensorFormatter.cs    sensor API, so each vendor is served by the SDK its
                                NvApiInterop.cs          own driver installs: NVIDIA temperature via NVAPI
                                NvmlInterop.cs           (nvapi_QueryInterface function-id dispatch, the
                                NvidiaGpuSensorReader.cs GpuAdapterProvider vtable technique) and power via
                                AdlInterop.cs            NVML; AMD temperature via ADL's PMLOG snapshot.
                                AmdGpuSensorReader.cs    Adapters are attributed by PCI identity, not LUID —
                                PnpPciParser.cs          the vendor SDKs report no LUID. No packages, no
                                                         admin. Every vendor and EVERY METRIC soft-fails to
                                                         "—" independently. AMD power + Intel are deferred —
                                                         see Deferred work above)
      /Storage                  StorageView.axaml(.cs) + StorageViewModel.cs
                                (LIVE — read-only drives/health view: a top row of DriveCard summary
                                 cards over a Partitions table (PartitionRow item VMs) + a Disk Activity
                                 card (shared Sparkline, ChartStorage amber). Page-scrolls like Network
                                 (not ISelfScrollingPage). Cards from PhysicalDiskProvider/StorageComposer/
                                 VolumeProvider; Disk Activity + Queue from the shared StorageUsageSampler
                                 feed; per-disk Read/Write from PhysicalDiskThroughputSampler; NVMe Temp
                                 from DiskTemperatureProvider (IOCTL health log). IRefreshablePage/
                                 ILiveSamplingPage/IDisposable.)
```

Feature-specific *providers* (static WMI/registry reads) live in the tab folder, not `src/Shared`,
until a second feature needs them (per the "keep each tab self-contained" rule). Live **sampling**,
however, is now shared: `SystemMetricsService` owns one sampler per metric and drives it through a
`MetricChannel` at 1 Hz, fanning each sample out to the pages that subscribe (Dashboard, Performance,
Processes). A subscriber keeps its own 60-sample rolling buffer (two for network — download + upload)
and rebuilds its `Sparkline` via `SparklinePoints.Build`, using `ChartScale.FitAxis` for the unbounded
network axis. Reuse these seams — do **not** re-inline a per-metric `DispatcherTimer` + `Array.Copy`
buffer or a bespoke points/peak helper.

The **System Information** panel reuses the same async-WMI provider pattern: `SystemInfoProvider`
(`GetAsync() => Task.Run(Read)`, `OperatingSystem.IsWindows()` guard, per-section soft-fail →
"Unknown …") reads the static identity facts once at startup into a `SystemStaticInfo` record. It
also reads the **registry** (via the in-box `Microsoft.Win32.Registry` API) for the build revision
(`UBR`) and feature-update label (`DisplayVersion`), which WMI does not expose. **Uptime** is the one
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
the accent + sets every chart key to that colour; `ApplyDefaultAppearance` restores the multi-colour look
(highlight blue, distinct graphs). It's constructed once in `MainWindowViewModel`, applied at startup, and
handed to `SettingsViewModel`. Theming is **session-only** (no persistence, by choice). Note this feature
deliberately touched shared styles + the shell (Palette/SharedStyles, MainWindow, NavItem) — theming is
cross-cutting, so it lives in `src/Services`, not a tab.

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
  `SystemMetricsService` (live sampling), `SparklinePoints` + `ChartScale` (charts),
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
`Microsoft.Win32.Registry` (the HKCU `Run` key) are in-box on `net10.0-windows`, and Avalonia's
`TrayIcon` ships with the framework. Reuse the in-box JSON + registry for future persisted state.

## Testing conventions

Unit tests live in **`tests/DashDetective.Tests`** (xUnit, `net10.0-windows`, referenced by
`DashDetective.sln`). CI runs them on `windows-latest` and collects coverage; `dotnet format` gates the
test code too, so keep usings alphabetical (`System` is **not** sorted first).

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
  - **Collapse/expand** — a **semi-circular puck** standing **entirely outside** the bar, touching its
    content-facing edge along the flat side only, revealed while the pointer is over the bar. It is a
    true half-disc: one radius deep, two long, both outward corners rounded by the full radius (no
    clamping). Its chevron points the way the bar will move (at the docked edge when expanded, away from
    it when collapsed). It is a sibling of the rail, not a child, and standing outside needs **two**
    things: `ClipToBounds="False"` on `NavigationView` (which otherwise clips to its docked slot and the
    puck vanishes) and **`ZIndex="1"`** on the bar in `MainWindow.axaml` (it is the `DockPanel`'s first
    child, so it would paint under the content area).
  - **Re-dock** — **right-click anywhere on the bar** for a "Dock navigation" menu at the pointer. The
    `ContextFlyout` is declared once on the rail `Border`: `ContextRequested` bubbles, so the brand, the
    items, the footer and any empty space all reach it.
  - **Re-dock by drag** — press and drag the **brand area** to the nearest window edge. The bar **dims
    in place** for the gesture while an accent drop band and a cursor chip preview the target edge.
  - **Settings → Appearance → Navigation** — Position + Collapse, both segmented controls.

  Orientation/collapse and every derived layout value (dock edge, rail thickness, item axis,
  label/brand/footer visibility, accent-indicator bar↔underline, scroll axis, the puck's size /
  alignment / stand-off / rounding) are **computed properties on the VM — no value converters**. The rail
  thickness has a **single owner**, `RailThickness(horizontal)`, which `RailWidth`/`RailHeight` delegate
  to and the drop preview measures against; it takes the axis as an argument because a drag previews
  edges the bar is not docked to yet. `MainWindowViewModel` owns page routing and delegates the bar to
  `Nav`, wiring `Nav.SelectionChanged` → `CurrentPage`. Orientation and collapse **persist** (see
  *Persistence* below); this is shared shell work, not a tab-local change.

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
  in **Mbps** (dual series on one shared scale + gradient fill) with a live adapter-name caption, via
  `NetworkUsageSampler` (managed `System.Net.NetworkInformation`, no P/Invoke — see the sampler note in
  *Folder Structure*). System Information: the whole panel now reads the real machine — **OS** edition +
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
    `ToolkitCatalog.Entries`, ranking the command text above its description), Processes (over the
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
    (multi-colour) option — a 2×2 four-colour square that restores the default look (each dashboard
    graph its own colour, highlight blue); the four single-colour swatches recolour **every** dashboard
    graph to that one accent.
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
    HKCU `…\Run` value via `StartupRegistration` (`src/Services/Startup`, soft-failing).
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
    This **supersedes the "session-only" note** for Theming and Navigation (their choices now persist).

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

- **Network** — **live and functional** (built in phases; plan:
  `C:\Users\User\.claude\plans\plan-and-brainstorm-how-iterative-wave.md`). Matches the design comp's
  Network page: six panels in two rows. The tab is always-on like the Dashboard (VM constructed once
  in `MainWindowViewModel`), reuses the shared `Sparkline`, and adds **no new NuGet packages** (all
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
    owning process names, via feature-local `iphlpapi` P/Invoke (`ConnectionsInterop` →
    `GetExtendedTcpTable`/`GetExtendedUdpTable`, IPv4 OWNER_PID tables) on a 2.5 s timer. Rows are
    **keyed-diffed** in place (no flicker); de-duplicated by identity key in `ConnectionsProvider`
    (two UDP sockets can share PID+local endpoint, which would otherwise break the diff), sorted,
    **capped at 100** with an honest "N active · showing 100" caption. PID→name is cached with
    stale-PID eviction; inaccessible/exited PIDs fall back to "PID n"; 0/4 → "System Idle"/"System".
  - **Ping** — continuous ping to a fixed `8.8.8.8` (in-box `Ping`, 2 s timer, 1.5 s timeout,
    in-flight-guarded), console-style last-3 replies + rolling avg-RTT / loss summary (`PingMonitor`).
  - **DNS Lookup** — one-shot resolve of a fixed `example.com` (in-box `Dns.GetHostEntryAsync`, 3 s
    `CancellationTokenSource`), run at startup and on Refresh (not a live loop), console-style output
    with record type (`DnsLookupProvider`).

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
  **The per-process Network ("NET") column was REMOVED BY DESIGN** (2026-07, branch
  `processesRemoveNET`) — there is no in-box, non-admin per-process network-rate API on Windows (Task
  Manager uses ETW kernel providers, needing the `TraceEvent` package + admin), so rather than ship a
  permanent "—" the column was deleted outright: header, data cell, sort key and all. This is **not
  deferred work** — do not re-add the column or build toward it without an explicit task. The table is
  7 columns. Follows the always-on tab pattern (constructed once in the shell; `IRefreshablePage` +
  `ILiveSamplingPage` + `IDisposable` + `ISelfScrollingPage`), the Network tab's keyed-diff live table
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
  selectable-item pattern (`NavItem` / `FilterOption`), shared styles, and fixed per-metric legend
  brushes. **All five resources are wired live**: each subscribes to the shared `SystemMetricsService`
  (CPU / Memory / Storage / GPU / Network), keeps its own 60-sample rolling history rebuilt via
  `SparklinePoints`, and pushes into the selected row; static hardware labels load once via the
  `*InfoProvider` async-WMI providers. Implements `IRefreshablePage` (toolbar Refresh re-samples every
  metric), `ILiveSamplingPage` (Live/Pause is the shared service's) and `IDisposable`. No new packages,
  no new shared controls. The CPU **Speed** tile is live: a page-local `ProcessorFrequencySampler`
  (`src/Services/SystemMetrics`) reads the PDH `\Processor Information(_Total)\% Processor Performance`
  ratio and `CpuSpeedFormatter` scales the WMI base clock (`CpuStaticInfo.MaxClockMhz`) by it — exactly
  Task Manager's Speed figure, so it rises above the base clock under Turbo (deliberately uncapped) and
  falls at idle. Pumped on the page-local throughput timer (fixed 1 Hz, not retimed by the Settings
  refresh interval) and on Refresh; degrades to "—" if the counter or base clock is unavailable. This is
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
  **left untouched**. Unlike the Speed tile it is read inside `UpdateMemory` on the shared memory tick,
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
  chart/format/diff duplication into `SparklinePoints`, `ChartScale`, `HardwareNameFormatter` and
  `CollectionReconciler`; added real shutdown disposal via a manual composition root in `App`; switched
  `NavigationView`/`MainWindow` fan-out to `[NotifyPropertyChangedFor]`; replaced the reflection
  `ViewLocator` with a compile-time switch; and added the soft-failing `Log` seam.