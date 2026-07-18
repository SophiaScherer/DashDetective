# DashDetective

[![.NET Desktop (Avalonia)](https://github.com/SophiaScherer/DashDetective/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/SophiaScherer/DashDetective/actions/workflows/dotnet-desktop.yml)

**DashDetective** is a Windows system-information console built with [Avalonia UI](https://avaloniaui.net/)
and .NET 10. It presents a Task-Manager-class view of the machine — live CPU, memory, GPU, storage and
network metrics; a read-only file browser; a network inspector; and a hardware spec sheet — behind a
single dockable, themeable shell. It reads the real machine through in-box Windows facilities (WMI, PDH
performance counters, and Win32 P/Invoke) with no elevation required, and is written to a strict
one-feature-at-a-time discipline: every tab is a self-contained module, and cross-cutting concerns
(theming, sampling, page lifecycle) live behind small shared seams.

> **Status:** actively built tab-by-tab. Dashboard, File Explorer, Network, Hardware and Settings →
> Appearance are live; Performance and Processes are in progress. See [Feature tour](#feature-tour)
> for the honest per-tab state and [Roadmap](#roadmap) for what's next.

## Screenshots

<!--
  SCREENSHOTS TO CAPTURE (save as PNG under docs/images/, names below):
    Per tab, in DARK theme with the default multi-colour accent:
      dashboard-dark.png, file-explorer-dark.png, processes-dark.png,
      performance-dark.png, network-dark.png, hardware-dark.png, settings-dark.png
    The same tabs in LIGHT theme:
      dashboard-light.png, file-explorer-light.png, processes-light.png,
      performance-light.png, network-light.png, hardware-light.png, settings-light.png
    One accent variant (pick a single accent swatch in Settings → Appearance, e.g. purple):
      dashboard-accent-purple.png
    The drag-to-dock navigation gesture (grab the nav bar and drag toward an edge so the
    docking hint chip + drop target are visible):
      nav-drag-to-dock.png
  Then replace the placeholder links below with the captured images.
-->

| Dashboard (dark) | File Explorer (dark) | Network (dark) |
| --- | --- | --- |
| ![Dashboard](docs/images/dashboard-dark.png) | ![File Explorer](docs/images/file-explorer-dark.png) | ![Network](docs/images/network-dark.png) |

| Hardware (light) | Performance (dark) | Settings — accent (purple) |
| --- | --- | --- |
| ![Hardware](docs/images/hardware-light.png) | ![Performance](docs/images/performance-dark.png) | ![Settings accent](docs/images/dashboard-accent-purple.png) |

Drag-to-dock navigation gesture:

![Drag to dock](docs/images/nav-drag-to-dock.png)

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

### Processes — *in progress*
A Task-Manager-style live process view: the list split into **Apps** and **Background processes**, with
per-process PID / status / CPU % / Memory / Disk / GPU %, sortable headers, a summary strip, **End
task**, and native **Properties**. Data is entirely in-box and needs no admin
(`System.Diagnostics.Process`, `GetProcessIoCounters` P/Invoke, PDH GPU counters). *The scaffold and
activation are in place; the live table lands in subsequent phases. Per-process network throughput is
deferred (no clean in-box per-process rate API).*

### Performance — *UI in progress (static mock data)*
A Task-Manager-style resource drill-down: a left **resource-selector rail**
(CPU · Memory · Disk · GPU · Ethernet) swaps a right **detail pane** — one large utilization chart
(fixed 0–100 axis, gradient fill) plus a four-tile stat strip. *This is a UI pass on static mock data;
live samplers and accent-reactive metrics are a later technical pass.*

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
- **Finish Performance & Processes** — swap Performance's mock data for live samplers, and land the
  Processes live table.
