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

**Implementation status within the active features:**

- **Dashboard** — the **CPU, Memory, GPU and Storage surfaces are live and functional**. CPU: the
  CPU `StatCard`, the "CPU Utilization" panel, and the System Information **CPU** and **Cores**
  rows. Memory: the Memory `StatCard`, the "Memory Utilization" panel, and the System
  Information **RAM** row all read the real machine. GPU: the GPU `StatCard` (live utilisation
  % + sparkline via PDH) and the System Information **GPU** row (adapter name via WMI); GPU
  **temperature** and **multi-GPU** layout are **deferred and out of scope for now** (research
  notes under *Deferred Dashboard work* below). Storage: the Storage `StatCard` shows live disk
  **Active time %** (headline value + sparkline, both from PDH `\PhysicalDisk(_Total)\% Idle Time`
  as `100 − idle`), with a system-drive capacity caption (`used / total` via `System.IO.DriveInfo`,
  no WMI). Everything else on the Dashboard (the **Network** card and its throughput chart, plus the
  remaining System Information rows) is **still static mock data** from the design doc — leave it
  alone unless a task explicitly asks to wire it up.
- **Settings** — still entirely layout-only (static `Border`s standing in for controls; the
  `SettingsViewModel` is empty).

**Everything else (File Explorer, Processes, Performance, Network, Storage, Hardware) is
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
      /Styles
        Palette.axaml           (colour brushes; merged in App.axaml)
        SharedStyles.axaml      (reusable class styles: card, panel, seg, toggle, buttons…)
      /Controls
        Sparkline, StatCard, InfoRow   (reusable widgets; Sparkline auto-fits to its data
                                        by default, or set YMin/YMax for a fixed axis —
                                        StatCard forwards YMin/YMax to its inner sparkline)
    /Shell                      (the app frame — the "default window")
      MainWindow.axaml(.cs), MainWindowViewModel.cs, ViewLocator.cs
      /Navigation
        NavItem.cs, Icons.cs
    /Tabs                       (one self-contained folder per feature)
      /Dashboard                DashboardView.axaml(.cs) + DashboardViewModel.cs
                                CpuUsageSampler.cs      (live total CPU % via GetSystemTimes)
                                CpuInfoProvider.cs      (static CPU info via WMI, async)
                                CpuStaticInfo.cs        (record for the WMI result)
                                MemoryUsageSampler.cs   (live RAM % + used/total via GlobalMemoryStatusEx)
                                MemoryInfoProvider.cs   (static RAM info via WMI, async)
                                MemoryStaticInfo.cs     (record for the WMI result)
                                GpuUsageSampler.cs      (live total GPU % via PDH GPU Engine counters)
                                GpuInfoProvider.cs      (static GPU name via WMI, async)
                                GpuStaticInfo.cs        (record for the WMI result)
                                StorageUsageSampler.cs  (live disk Active time % via PDH PhysicalDisk
                                                         counters; capacity caption uses DriveInfo, no WMI)
      /Settings                 SettingsView.axaml(.cs) + SettingsViewModel.cs
      (FileExplorer, Processes, Performance, Network, Storage, Hardware — not yet started)
```

Feature-specific helpers (samplers, providers) live in the tab folder, not `src/Shared`, until
a second feature needs them (per the "keep each tab self-contained" rule). The live-CPU and
live-Memory code above is the reference example: each metric has its own 1 Hz `DispatcherTimer`
and a 60-sample rolling buffer in `DashboardViewModel`, plus a feature-local sampler (Win32
P/Invoke) and WMI provider.

Namespaces follow folders: `DashDetective.Shared`, `DashDetective.Shared.Controls`,
`DashDetective.Shell`, `DashDetective.Shell.Navigation`, `DashDetective.Tabs.<Feature>`.
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
`Win32_PhysicalMemory`, etc.). Reuse it for future hardware queries. Adding any *new* package
still requires asking first (see Strict Working Boundaries).

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