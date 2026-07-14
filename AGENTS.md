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

**Currently active features (the only ones you may touch):**

- `Dashboard`
- `Settings`
- `File Explorer`
- `Network`
- `Processes`
- `Hardware`

**Implementation status within the active features** (condensed — the full write-ups live in
*Appendix — Completed Feature Details* at the end of this file):

- **Navigation bar (shell-level).** A self-contained **collapsible + dockable** sidebar —
  `NavigationView` + `NavigationViewModel` under `src/Shell/Navigation/`. Docks to any edge and
  collapses to an icons-only rail; orientation/collapse and every derived layout value are **computed
  properties on the VM — no value converters**. Driven from two entry points (on-bar controls +
  **Settings → Appearance**). `MainWindowViewModel` owns page routing via `Nav.SelectionChanged` →
  `CurrentPage`. State is **session-only**. *(full detail in Appendix)*

- **Dashboard** — **fully live**: CPU, Memory, GPU, Storage and Network surfaces plus the System
  Information panel all read the real machine (WMI + registry + PDH + managed samplers). The shell
  **toolbar** is wired: a live clock, a **Live** pill (pause/resume sampling via `ILiveSamplingPage`),
  a **Refresh** button (routes to the active page via `IRefreshablePage`), and an **Export** button
  (plain-text diagnostics via the in-box `Avalonia.Platform.Storage` picker). Only the toolbar
  **Search** box is still non-functional. GPU temperature + multi-GPU are deferred (see *Deferred
  Dashboard work*). *(full detail in Appendix)*

- **Settings** — **Appearance is live** (Theme segmented control + Accent swatches applied at runtime
  through the single `ThemeService` — see *Theming*; the first accent swatch is a "Default" multi-colour
  option). The **Monitoring** and **Export & Data** panels remain static layout. *(full detail in
  Appendix)*

- **File Explorer** — **live and functional**: a read-only three-pane browser (folder tree · file list
  with breadcrumb/filter chips/sortable headers/Show-hidden · details+preview), all from `System.IO`,
  with friendly type names + Properties via shell32 P/Invoke and themed vector glyphs. Live
  auto-refresh via a debounced `FileSystemWatcher`; contextual Refresh via `IRefreshablePage`;
  independent per-pane scrolling + resizable panes via the shell's `ISelfScrollingPage` seam +
  `GridSplitter.paneSplitter`. Introduced the app's first `TreeView`. *(full detail in Appendix)*

- **Network** — **live and functional**: six panels (Adapters · IP Configuration · Throughput · Active
  Connections · Ping · DNS Lookup), all in-box (`System.Net.NetworkInformation`, `Ping`, `Dns`,
  `iphlpapi` P/Invoke). Always-on like the Dashboard; reuses the shared `Sparkline`; keyed-diff live
  connections table. Added the shared `NetworkUsageSampler` (in `src/Services/Network`) and the
  `ILiveSamplingPage` seam. IPv6 connections deferred. *(full detail in Appendix)*

- **Processes** — **being built in phases**: a Task-Manager-style live process view (Apps/Background/
  Windows groups; per-process PID/status/CPU/Memory/Disk/GPU; sortable headers; summary strip; End
  task; native Properties). In-box, no admin (`System.Diagnostics.Process`, `GetProcessIoCounters`
  P/Invoke, PDH `\GPU Engine`). Per-process Network is deferred. Follows the always-on tab pattern +
  keyed-diff table. *(full detail in Appendix)*

- **Hardware** — **newly activated; UI-only, being built in phases** (plan:
  `C:\Users\User\.claude\plans\develop-a-plan-to-iridescent-pearl.md`). Per the design comp: a
  2-column grid of six spec cards (Processor, Graphics, Motherboard, Memory, Storage Devices, Sensors),
  each an icon-tile header + key/value spec rows. This phase renders a **data-driven** static layout
  (`HardwareCard` + `HardwareSpec` records → an `ItemsControl` grid reusing the shared `InfoRow`
  control) with **neutral placeholders** (`—`); the page scrolls as a whole like the Dashboard (not
  self-scrolling). **Live/real data (WMI/PDH/sensors) is deferred** to a later technical phase, so
  there is no `IRefreshablePage`/`ILiveSamplingPage` wiring yet.

**Everything else (Performance, Storage) is
out of scope until this document says otherwise.** Do not scaffold, stub, reference, or
"prepare" folders for inactive features, even if it seems convenient or efficient. Wait until
they are explicitly activated in a future revision of this file.

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
      /Theming
        ThemeService.cs         (single seam that applies theme + accent to Application at runtime)
        AppTheme.cs             (enum: System / Light / Dark)
        AccentPreset.cs         (record: one accent's Color/Hover/OnAccent/Deep; .All = the four)
      /SystemMetrics
        CpuUsageSampler.cs      (live total CPU % via GetSystemTimes; shared — Dashboard + Processes)
        MemoryUsageSampler.cs   (live RAM % + used/total via GlobalMemoryStatusEx; shared —
                                 Dashboard + Processes. Both moved here from src/Tabs/Dashboard with
                                 sign-off when the Processes tab needed the same system-wide readings,
                                 the same precedent as NetworkUsageSampler. GPU/Storage samplers stay
                                 in the Dashboard until a second tab needs them.)
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
                                GpuUsageSampler.cs      (live total GPU % via PDH GPU Engine counters)
                                GpuInfoProvider.cs      (static GPU name via WMI, async)
                                GpuStaticInfo.cs        (record for the WMI result)
                                StorageUsageSampler.cs  (live disk Active time % via PDH PhysicalDisk
                                                         counters; capacity caption uses DriveInfo, no WMI)
                                (NetworkUsageSampler.cs is now under src/Services/Network — shared with
                                 the Network tab; the Dashboard VM still owns its own instance)
                                SystemInfoProvider.cs   (static system identity — OS/device/BIOS/board/build —
                                                         via WMI + registry, async; uptime is live off
                                                         Environment.TickCount64 in the VM, no sampler file)
                                SystemStaticInfo.cs     (record for the system-identity result)
      /Settings                 SettingsView.axaml(.cs) + SettingsViewModel.cs
                                ThemeOption.cs, AccentOption.cs  (selectable item VMs for the
                                                                  Appearance controls, like NavItem)
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
                                                        (static UI-only spec grid; whole-page scroll
                                                         like the Dashboard — not self-scrolling. VM
                                                         exposes a fixed list of HardwareCard models
                                                         reused by an ItemsControl; live data deferred)
                                HardwareCard.cs         (record: title/subtitle/icon/colours + rows)
                                HardwareSpec.cs         (record: one key/value spec row; value → "—")
                                HardwareIcons.cs        (feature-local card glyph geometries + fixed
                                                         per-card icon colours)
      (Performance, Storage — not yet started)
```

Feature-specific helpers (samplers, providers) live in the tab folder, not `src/Shared`, until
a second feature needs them (per the "keep each tab self-contained" rule). The live-CPU and
live-Memory code above is the reference example: each metric has its own 1 Hz `DispatcherTimer`
and a 60-sample rolling buffer in `DashboardViewModel`, plus a feature-local sampler (Win32
P/Invoke) and WMI provider. The Network metric follows the same pattern but keeps **two** 60-sample
buffers (download + upload) and computes a shared dynamic `YMax` so both series plot on one scale.

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

- **Settings** — the **Appearance** section is now live; the rest is still layout-only. The
  **Theme** segmented control (Dark / Light / System) and the **Accent color** swatches are
  data-bound to `SettingsViewModel` and applied at runtime through a single `ThemeService` (see
  *Theming* below). The accent row's **first** swatch is a "Default" (multi-colour) option — a 2×2
  four-colour square that restores the default look (each dashboard graph its own colour, highlight
  blue); the four single-colour swatches recolour **every** dashboard graph to that one accent. The
  **Monitoring** panel (interval segments + toggle pills) and **Export & Data** buttons remain static
  `Border`s, not yet wired.

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