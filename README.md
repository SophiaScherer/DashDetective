# DashDetective

[![.NET Desktop (Avalonia)](https://github.com/SophiaScherer/DashDetective/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/SophiaScherer/DashDetective/actions/workflows/dotnet-desktop.yml)

**DashDetective** is a Windows system-information console built with [Avalonia UI](https://avaloniaui.net/)
and .NET 10. It presents a Task-Manager-class view of the machine — live CPU, memory, GPU, storage and
network metrics; a read-only file browser; a network inspector; and a hardware spec sheet — behind a
single dockable, themeable shell. It reads the real machine through in-box Windows facilities (WMI, PDH
performance counters, and Win32 P/Invoke) with no elevation required, and is written to a strict
one-feature-at-a-time discipline: every tab is a self-contained module, and cross-cutting concerns
(theming, sampling, page lifecycle) live behind small shared seams.

> **Status:** actively built tab-by-tab. Dashboard, File Explorer, Processes, Performance, Network and
> Hardware are live; in Settings, Appearance and Navigation are live while Monitoring and Export are
> still static layout. See [Feature tour](#feature-tour) for the honest per-tab state and
> [Roadmap](#roadmap) for what's next.

[//]: # (## Screenshots)

[//]: # ()
[//]: # (<!--)

[//]: # (  Screenshots live in docs/images/ &#40;captured from the running app, maximised at 1936x1080&#41;.)

[//]: # (  The full set is available there; the grid below shows a representative selection:)

[//]: # (    Every tab in both themes: <tab>-dark.png and <tab>-light.png for)

[//]: # (      dashboard, file-explorer, processes, performance, network, hardware, settings)

[//]: # (    Accent variant:  dashboard-accent-purple.png  &#40;dark theme, purple accent&#41;)

[//]: # (    Nav gesture:     nav-drag-to-dock.png          &#40;bar lifted, "Dock top" hint chip visible&#41;)

[//]: # (  To refresh them, re-run the capture &#40;see docs/images&#41; after a Release build; keep the file names)

[//]: # (  stable so these links keep resolving.)

[//]: # (-->)

[//]: # ()
[//]: # (| Dashboard &#40;dark&#41; | File Explorer &#40;dark&#41; | Network &#40;dark&#41; |)

[//]: # (| --- | --- | --- |)

[//]: # (| ![Dashboard]&#40;docs/images/dashboard-dark.png&#41; | ![File Explorer]&#40;docs/images/file-explorer-dark.png&#41; | ![Network]&#40;docs/images/network-dark.png&#41; |)

[//]: # ()
[//]: # (| Hardware &#40;light&#41; | Performance &#40;dark&#41; | Settings — accent &#40;purple&#41; |)

[//]: # (| --- | --- | --- |)

[//]: # (| ![Hardware]&#40;docs/images/hardware-light.png&#41; | ![Performance]&#40;docs/images/performance-dark.png&#41; | ![Settings accent]&#40;docs/images/dashboard-accent-purple.png&#41; |)

[//]: # (Drag-to-dock navigation gesture:)

[//]: # ()
[//]: # (![Drag to dock]&#40;docs/images/nav-drag-to-dock.png&#41;)

## Feature tour

DashDetective is organised as a set of tabs behind a dockable navigation bar. Each is a self-contained
feature folder under `DashDetective/src/Tabs/`.

### Dashboard — *live*
An at-a-glance overview of the machine. Every surface reads the real hardware:
- **Live stat cards + utilization charts** for CPU, Memory, GPU, Storage (disk active-time %) and
  Network (download/upload in Mbps), each on its own 1 Hz sampler with a 60-sample rolling sparkline.
- **System Information** panel — OS edition + feature update, device name, BIOS, motherboard, full
  build number, and a live-updating uptime (read once at startup via WMI + registry; uptime ticks off
  `Environment.TickCount64`).
- **Wired toolbar** — a live 24-hour clock, a **Live** pill that pauses/resumes all sampling, a
  **Refresh** button that re-reads whichever page is active, and an **Export** button that writes a
  plain-text diagnostics report via the native save dialog.

GPU temperature and multi-GPU layouts are researched but intentionally deferred (no universal Windows
API).

### File Explorer — *live*
A read-only three-pane file browser:
- **Folder tree** (drives as roots, lazily-loaded subfolders), a **file list** with a clickable
  breadcrumb, **filter chips** (All / Documents / Images / Archives), **sortable column headers**, and
  a **Show hidden** toggle.
- **Details / preview pane** with Type / Size / Modified / Created / Attributes / Location and **Open**
  + **Properties** (the native Windows property sheet) actions.
- **Live auto-refresh** — a debounced `FileSystemWatcher` keeps the open folder current as the disk
  changes, preserving selection and scroll position. Panes scroll independently and are user-resizable.

Built on `System.IO` with per-entry soft-fail off the UI thread; friendly type names and shell actions
via `shell32` P/Invoke.

### Processes — *live*
A Task-Manager-style live process view: the list split into **Apps** and **Background processes**, with
per-process PID / status / CPU % / Memory / Disk / GPU %, sortable headers, a summary strip (process
counts, total CPU/memory, thread count), **End task**, and native **Properties**. Data is entirely
in-box and needs no admin (`System.Diagnostics.Process`, `GetProcessIoCounters` P/Invoke, PDH GPU
counters), with the live table keyed-diffed in place. *Per-process network throughput is deferred (the
**Net** column shows `—`) — there is no clean in-box per-process rate API.*

### Performance — *live*
A Task-Manager-style resource drill-down: a left **resource-selector rail** (CPU · Memory · Disk · GPU ·
network adapter) with a live value per resource swaps a right **detail pane** — one large utilization
chart (fixed 0–100 axis, gradient fill) plus a four-tile stat strip. Each metric runs on its own 1 Hz
sampler, mirroring the Dashboard, and honours the toolbar Live/Refresh controls.

### Network — *live*
A six-panel network inspector:
- **Adapters** — every adapter (physical + virtual) with a status dot, state and link speed.
- **IP Configuration** — the primary adapter's IPv4 / mask / gateway / DNS / MAC / DHCP.
- **Throughput** — live download/upload in Mbps as dual sparklines.
- **Active Connections** — a netstat-style TCP + UDP table with owning process names (via `iphlpapi`
  P/Invoke), keyed-diffed in place and capped at 100.
- **Ping** — a continuous ping to `8.8.8.8` with rolling average RTT and loss.
- **DNS Lookup** — a one-shot resolve of `example.com` with the record type.

All in-box (`System.Net.NetworkInformation`, `Ping`, `Dns`, `iphlpapi`). IPv6 connections are deferred.

### Hardware — *live*
A spec sheet of cards — **Processor**, **Graphics**, **Motherboard**, **Memory**, **Storage Devices**
and **Sensors** — populated from WMI, with a rated-spec catalog filling in details WMI can't report.
The **Sensors** card is a placeholder (`—`) pending a sensor source.

### Settings — *Appearance live; rest static*
- **Appearance** (live) — a **Theme** control (Dark / Light / System) and **Accent color** swatches,
  applied at runtime through a single `ThemeService`. The first accent is a **Default** multi-colour
  option (each dashboard graph its own colour); the others recolour every graph to one accent.
- **Navigation** (live) — Position (dock edge) and Collapse controls that drive the same shared
  navigation view-model as the on-bar controls.
- **Monitoring** and **Export & Data** are static layout, not yet wired.
- The footer shows the real assembly version (e.g. `DashDetective v0.1.0 · © 2026`), read from
  assembly metadata rather than a hard-coded string.

## Architecture at a glance

A fuller write-up lives in **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**; the essentials:

- **Single-window shell.** `MainWindow` is a `DockPanel` hosting a dockable/collapsible navigation bar
  plus a page host. A `ViewLocator` maps each `*ViewModel` to its `*View` by name.
- **Marker interfaces for page behaviour.** Instead of a heavyweight base class, pages opt into shell
  behaviours by implementing small interfaces in `src/Shared`:
  - `ISelfScrollingPage` — the page fills the viewport and scrolls its own panes (the shell does not
    wrap it in a scroll region).
  - `IRefreshablePage` — the toolbar **Refresh** routes to `Refresh()`.
  - `ILiveSamplingPage` — the toolbar **Live** pill pauses/resumes the page's sampling.
- **Feature-folder layout.** Source is split three ways:
  - `src/Shared` — feature-agnostic building blocks (view-model base, the marker interfaces, reusable
    controls like `Sparkline` / `StatCard` / `InfoRow`, styles and palette).
  - `src/Services` — cross-cutting services (theming, system-metric samplers, network sampler) that
    are shared by more than one tab.
  - `src/Tabs/<Feature>` — one self-contained folder per tab (view, view-model, and its
    feature-local helpers). A helper moves up to `Services`/`Shared` only once a second tab needs it.
- **Sampler / provider soft-fail convention.** *Samplers* produce live values on a timer; *providers*
  read static facts once, off the UI thread (`Task.Run`), behind an `OperatingSystem.IsWindows()`
  guard. Both degrade to neutral fallbacks ("—", "Unknown …") rather than throwing, so one dead source
  never blanks a whole page.
- **`ThemeService` — the single theming seam.** It is the only code that writes theme/accent to
  `Application.Current`. Anything that can change at runtime is referenced with `{DynamicResource}`.
  Theming is session-only by design.

## Build & run

DashDetective targets **`net10.0-windows`** and is **Windows-only** — it depends on facilities that
only exist on Windows: **WMI** (`System.Management`) for static hardware identity, **PDH performance
counters** for live GPU/disk metrics, the **registry** for build details, and various **Win32
P/Invoke** (`shell32`, `iphlpapi`) for shell integration and the connections table. There is no
cross-platform fallback by design.

**Prerequisites**
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10/11

**Run**
```powershell
dotnet run --project DashDetective
```

**Build**
```powershell
dotnet build DashDetective.sln -c Release
```

The build treats warnings as errors and enforces code style from `.editorconfig`
(`EnforceCodeStyleInBuild`). Before pushing, verify formatting the same way CI does:
```powershell
dotnet format DashDetective.sln --verify-no-changes
```

### Continuous integration
The [.NET Desktop (Avalonia)](.github/workflows/dotnet-desktop.yml) workflow restores, **verifies
formatting** (`dotnet format --verify-no-changes`), builds Debug + Release, runs tests (no test
project yet — see the Roadmap), and publishes the Release build as a downloadable artifact. It runs on
`windows-latest`.

## Project layout

```
DashDetective/
  Program.cs, App.axaml(.cs)      bootstrap
  src/
    Shared/                       marker interfaces, ViewModelBase, AppInfo, Controls, Styles
    Services/                     Theming (ThemeService), SystemMetrics + Network samplers
    Shell/                        MainWindow, MainWindowViewModel, ViewLocator, Navigation
    Tabs/                         Dashboard, FileExplorer, Processes, Performance, Network, Hardware, Settings
docs/
  ARCHITECTURE.md                 reader-facing architecture doc
.github/workflows/                CI
```

## Roadmap

Honest near-term work, roughly in priority order:

- **Settings persistence** — theme, accent, nav position and pane sizes are session-only today; persist
  them across launches.
- **Automated tests** — there is no test project yet; the CI test step is a no-op placeholder. Add unit
  coverage for the formatters, catalogs and sampler math.
- **Storage tab** — a dedicated storage view (per-volume capacity, activity, SMART) is planned but not
  started.
- **Hardware sensors** — wire the **Sensors** card to a real temperature/fan source (needs vendor SDKs
  or a sensor library), and revisit deferred GPU temperature / multi-GPU layouts.
- **Deferred metrics** — per-process **network throughput** in Processes and **IPv6** active
  connections in Network both await a suitable in-box data source.
