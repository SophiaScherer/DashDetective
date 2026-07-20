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
- Settings

Not all of these exist yet. Only build what is listed below as "currently active."

## Current Scope — READ THIS FIRST

**The feature currently being built (the only one you may modify):**

- `Performance` — initial UI implementation, **static mock data** (status below).

**Repo-hygiene / portfolio pass — COMPLETED (2026-07-18), under explicit sign-off.** A one-off,
cross-cutting pass outside the usual per-feature boundaries was authorised and is done: a portfolio
`README.md`, a reader-facing `docs/ARCHITECTURE.md` (distilled from the appendix below), project
metadata in the csproj (`Version 0.1.0`, title/authors/copyright, retarget to `net10.0-windows`),
analyzer + warning gates (`AnalysisLevel latest`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`)
with a root `.editorconfig` encoding the existing style, a `dotnet format --verify-no-changes` step in
CI, and the Settings footer wired to a real assembly version via `AppInfo` (`src/Shared`) instead of the
old fictional string. This did **not** change any feature behaviour (the footer text is the sole
exception). It does not widen the working boundaries: scope control still proceeds **phase-by-phase per
the roadmap**, and any further cross-cutting or out-of-feature change still needs its own explicit
sign-off.

**De-duplication / composition refactor — COMPLETED (2026-07-19), under explicit sign-off.** A
cross-cutting pass over `src/Shared`, `src/Services`, `src/Shell` and the Dashboard / Performance /
Network / Processes tabs, with **zero user-visible behaviour change**. It replaced the ~10× copy-pasted
per-metric `DispatcherTimer` + rolling-buffer pattern with `MetricChannel` + a shared
`SystemMetricsService` (one sampler set, ref-counted subscriptions, removing the duplicate PDH GPU/disk
queries); consolidated the chart/format/diff duplication into `SparklinePoints`, `ChartScale`,
`HardwareNameFormatter` and `CollectionReconciler`; added real shutdown disposal via a manual composition
root in `App`; switched `NavigationView`/`MainWindow` fan-out to `[NotifyPropertyChangedFor]`; replaced
the reflection `ViewLocator` with a compile-time switch; and added the soft-failing `Log` seam. As with
the portfolio pass, this did not widen the working boundaries — further cross-cutting work still needs
its own sign-off.

**Already-live features — read for consistency (shared styles, naming, the always-on / self-scrolling
patterns), but do NOT modify while building Performance** (full write-ups in *Appendix — Completed
Feature Details*): the shell **Navigation bar**, **Dashboard**, **Settings** (fully live — Appearance,
Navigation, Monitoring and Export & Data), **File Explorer**, **Network**, **Processes**, **Hardware**.
Editing any of these needs an explicit scope expansion.

**Performance — implementation status** (the already-live features' write-ups live in *Appendix —
Completed Feature Details* at the end of this file):

- **Performance** — **initial UI in progress**, built in phases (plan:
  `C:\Users\User\.claude\plans\develop-a-plan-to-elegant-thimble.md`). A Task-Manager-style live
  resource drill-down per the design comp: a left **resource selector** rail (CPU · Memory · Disk 0 (C:)
  · GPU · Ethernet) that swaps a right **detail pane** — one large utilization chart (reuses the shared
  `Sparkline`, fixed 0–100 axis + gradient fill, **no grid**) plus a 4-tile stat strip. Self-contained
  tab under `src/Tabs/Performance/` (`PerformanceView` + `PerformanceViewModel`, `ISelfScrollingPage`
  master-detail like File Explorer), reusing the selectable-item pattern (`NavItem` / `FilterOption`),
  shared styles, and the `Chart*` palette keys. **This UI pass is static mock data only** — live
  samplers/providers, real metrics, `IRefreshablePage` / `ILiveSamplingPage`, and accent-reactive
  brushes are a **later technical pass**, not built yet. No new packages, no new shared controls.

**Everything else (Storage) is out of scope until this document says otherwise.** Do not scaffold, stub,
reference, or "prepare" folders for inactive features, even if it seems convenient or efficient. Wait
until they are explicitly activated in a future revision of this file.

### Deferred Dashboard work — DO NOT build without an explicit task

These were scoped and researched but are **intentionally not built**. The notes exist so the
research isn't lost, not as a licence to start. Leave the GPU card as a single fixed card until
a task explicitly reactivates this. Full plan:
`C:\Users\User\.claude\plans\i-was-in-the-iridescent-pretzel.md`.

- **GPU temperature** — would append `· <temp>°C` to the GPU card caption. No universal Windows
  API; needs vendor SDKs (NVML / ADLX / IGCL) or a library, best-effort with graceful fallback.

- **Multi-GPU layout** — on multi-GPU machines, render one card per GPU via a dynamic
  `ObservableCollection` + `ItemsControl` in a single wrapping row, relocating the Storage/Network
  cards to reflow. This is an **architecture change** (per the boundaries below, stop and get
  sign-off first). Research findings from a part-built, since-discarded attempt:
  - Per-GPU utilisation must be **attributed by adapter LUID**: the PDH `\GPU Engine(*)` and
    `\GPU Adapter Memory(*)` counter instances are keyed by `luid_0x{High:x8}_0x{Low:x8}` but carry
    no friendly name.
  - **DXGI** (`dxgi.dll`, `CreateDXGIFactory1` → `EnumAdapters1` → `GetDesc1`) is the authoritative
    LUID→name map; it also reports **true VRAM** (WMI `AdapterRAM` is capped at 4 GB) and flags
    software adapters. It **must be called via raw vtable function pointers, not `[ComImport]`** —
    built-in COM is disabled by a runtime feature switch (`NotSupportedException: Built-in COM has
    been disabled`). The vtable approach needs no `unsafe` and no csproj change.
  - `Win32_VideoController` has **no utilisation counter** and its `AdapterRAM` is 4 GB-capped, so
    it's only good for the name; filter to physical adapters by `PNPDeviceID` starting with `PCI\`.
  - The correct card set is **the LUIDs present in the PDH counters ∩ the DXGI adapter names, minus
    software adapters**. DXGI can list one physical GPU under several LUIDs and also enumerates a
    "Microsoft Basic Render Driver" (software) — both are noise to be discarded.

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
        CpuUsageSampler.cs      (live total CPU % via GetSystemTimes)
        MemoryUsageSampler.cs   (live RAM % + used/total via GlobalMemoryStatusEx)
        GpuUsageSampler.cs      (live total GPU % via PDH GPU Engine counters; owns a PDH query handle)
        StorageUsageSampler.cs  (live disk Active time % + read/write/response via PDH PhysicalDisk
                                 counters; owns a PDH query handle)
        DiskInfoProvider.cs     (static primary-disk model/type/capacity via WMI, async)
        MetricChannel.cs        (reusable "sampler + DispatcherTimer + rolling double[window] history"
                                 unit — one try/catch per tick → onFailed + permanent stop; SampleNow for
                                 paused Refresh. Non-generic MetricChannel for plain-double metrics,
                                 generic MetricChannel<TSample> for snapshot samples + a no-history variant)
        SystemMetricsService.cs (SINGLE owner of the five samplers; per-metric 1 Hz channel fans each
                                 sample out to subscribers (ref-counted — a channel runs only while it has
                                 one), Pause/Resume for the Live pill, RefreshAll for Refresh, per-metric
                                 fault isolation. Dashboard / Performance / Processes SUBSCRIBE instead of
                                 owning samplers — this removes the duplicate PDH GPU/disk queries. The
                                 Network tab keeps its own NetworkUsageSampler. Built in the App composition
                                 root and disposed on shutdown.)
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
        NavigationViewModel.cs       footer + on-bar collapse/kebab controls. The VM owns Orientation +
                                     IsCollapsed and exposes all layout as computed properties — Dock,
                                     Rail sizes, ItemsOrientation, Hairline edge, scroll axis — no
                                     converters. Selection/layout visuals are styled in
                                     NavigationView.axaml via DynamicResource so they follow theme + accent)
        NavItem.cs, Icons.cs        (NavItem is a pure data model; Icons holds the glyph geometries)
        NavOrientation.cs           (enum: the dock edge — Left/Right/Top/Bottom)
        NavPositionOption.cs        (selectable item VM for the position picker, like NavItem/ThemeOption)
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
                                (ACTIVE BUILD — initial UI, static mock data. Task-Manager-style
                                 master-detail: a 220px resource-selector rail (ResourceRow item VMs)
                                 swaps a right detail pane — one large Sparkline utilization chart +
                                 a 4-tile stat strip (StatTile item VMs). Fills the viewport via
                                 ISelfScrollingPage, like File Explorer. Built in phases — item VMs
                                 (ResourceRow/StatTile) land in later UI phases; live samplers/
                                 providers + IRefreshablePage/ILiveSamplingPage are a later
                                 technical pass. See Current Scope + the plan file.)
      (Storage — not yet started)
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

The System Information work reads the **registry** via the `Microsoft.Win32.Registry` API (build
revision + feature-update label). On the `net10.0` target this API is **provided in-box — no package
reference is needed** (adding the `Microsoft.Win32.Registry` package is redundant and raises an
`NU1510` "unnecessary" warning). So it, too, added **no** new dependency.

The **Settings persistence** work (settings store + "Launch at startup" + system tray) likewise added
**no** new package: `System.Text.Json` (source-generated `SettingsJsonContext`) and
`Microsoft.Win32.Registry` (the HKCU `Run` key) are in-box on `net10.0-windows`, and Avalonia's
`TrayIcon` ships with the framework. Reuse the in-box JSON + registry for future persisted state.

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
  icons-only rail**, in any orientation. Two entry points drive the **same shared**
  `NavigationViewModel`: on-bar controls (a collapse chevron + a three-dot **kebab** menu whose
  `Flyout` — rendered in the window overlay layer, so it is **never clipped** by the rail — offers the
  four dock positions), and a **Navigation** group in **Settings → Appearance** (Position + Collapse,
  both segmented controls). Orientation/collapse and every derived layout value (dock edge, rail
  thickness, item axis, label/brand/footer visibility, accent-indicator bar↔underline, scroll axis)
  are **computed properties on the VM — no value converters**. `MainWindowViewModel` owns page routing
  and delegates the bar to `Nav`, wiring `Nav.SelectionChanged` → `CurrentPage`. State is
  **session-only** (resets to Left/expanded each launch, like Theming); this is shared shell work, not
  a tab-local change.

- **Dashboard** — the **CPU, Memory, GPU, Storage and Network surfaces are live and functional**. CPU:
  the CPU `StatCard`, the "CPU Utilization" panel, and the System Information **CPU** and **Cores**
  rows. Memory: the Memory `StatCard`, the "Memory Utilization" panel, and the System
  Information **RAM** row all read the real machine. GPU: the GPU `StatCard` (live utilisation
  % + sparkline via PDH) and the System Information **GPU** row (adapter name via WMI); GPU
  **temperature** and **multi-GPU** layout are **deferred and out of scope for now** (research
  notes under *Deferred Dashboard work* below). Storage: the Storage `StatCard` shows live disk
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
  needs the window's `TopLevel`). The toolbar **Search** box is still non-functional (deferred). Export
  uses the in-box `Avalonia.Platform.Storage` picker — no new package.

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

- **Processes** — **newly activated; being built in phases** (plan:
  `C:\Users\User\.claude\plans\processes-tab-plan.md`). Intended as a Task-Manager-style live process
  view: the list **split into Apps and Background processes**, per-process **PID / status / CPU % /
  Memory / Disk / (Network — deferred) / GPU %**, **sortable column headers**, a summary strip
  (**process counts per group**, **total CPU %**, **total Memory %**, **total thread count**), **End
  task**, and native **Properties** (the exe's shell property sheet). Data is **in-box, no new
  dependencies, no admin**: `System.Diagnostics.Process` (CPU% via `TotalProcessorTime` diff, memory,
  threads, status, Apps/Background split via `MainWindowHandle`, exe path), a feature-local
  `GetProcessIoCounters` P/Invoke for Disk MB/s, and PDH `\GPU Engine(*)` grouped by the `pid_` token for
  GPU %. **Per-process Network throughput is deferred** — there is no clean in-box per-process rate API
  (Task Manager uses ETW kernel providers, which need the `TraceEvent` package + admin); the Network
  column renders "—" until a task reactivates it. Follows the always-on tab pattern (constructed once in
  the shell; `IRefreshablePage` + `ILiveSamplingPage` + `IDisposable` + `ISelfScrollingPage`), the Network
  tab's keyed-diff live table, and the File Explorer sortable-header + Properties patterns. *Phase 0
  (scaffold + activation) is in place; the live table and features land in later phases.*