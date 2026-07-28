# DashDetective — Architecture

This document explains how DashDetective is put together: the shell, how pages are hosted and kept
alive, the sampler/provider conventions behind the live data, the theming seam, settings persistence,
the shared control inventory, and the seams that make the whole thing testable. It is a reader-facing
distillation of the project's internal working notes — enough to find your way around the code without
reading every file.

Build, run and test instructions live in the [README](../README.md).

DashDetective is an [Avalonia UI](https://avaloniaui.net/) desktop app on `net10.0-windows`, using the
MVVM pattern with `CommunityToolkit.Mvvm`. It is Windows-only on purpose (WMI, PDH performance
counters, registry, and Win32 P/Invoke).

## Guiding principles

- **One feature at a time, each self-contained.** Every tab lives in its own folder with its view,
  view-model and feature-local helpers. A helper is promoted to a shared location only when a *second*
  feature needs it — not pre-emptively.
- **Small seams over big base classes.** Shell behaviours are opt-in via marker interfaces, not a
  heavyweight page base class.
- **Soft-fail everywhere.** Reading the machine can fail (denied access, a non-Windows host, a
  vanishing process). Every reader degrades to a neutral fallback instead of throwing.

## Source layout

Source lives under `DashDetective/src/`, split into four areas. Namespaces follow folders
(`DashDetective.Shared`, `DashDetective.Services.Theming`, `DashDetective.Shell`,
`DashDetective.Tabs.<Feature>`, …).

| Area | Holds |
| --- | --- |
| `src/Shared` | Cross-cutting, feature-agnostic building blocks: `ViewModelBase`, the marker interfaces, `AppInfo`, reusable controls, styles and the colour palette, the pure-logic `Charts` helpers (`ChartScale`, `SparklinePoints`) and formatters (`DataRateFormatter`, `UptimeFormatter`, `HardwareNameFormatter`, `CollectionReconciler`). |
| `src/Services` | Cross-cutting services shared by more than one tab: `Theming` (the `ThemeService` seam), `SystemMetrics` (CPU/Memory/GPU/Storage samplers and providers), `Network` (the shared throughput sampler), `Settings` (the persistence store), `Startup` (launch-at-startup registration), `Threading` (the `IUiTimer` seam), `Identity` and `Diagnostics`. |
| `src/Shell` | The application frame: `MainWindow`, `MainWindowViewModel`, `ViewLocator`, and the dockable `Navigation` bar. |
| `src/Tabs/<Feature>` | One folder per tab (Dashboard, FileExplorer, Processes, Performance, Network, Storage, Hardware, Settings). |

**Rule of thumb:** anything reused by more than one tab (a control, a colour, a sampler) belongs in
`Shared`/`Services`; everything else stays inside its tab folder.

## The shell and navigation

`MainWindow`'s root is a `DockPanel` that hosts the navigation bar at the user-chosen edge, plus the
main content area. `MainWindowViewModel` owns page routing: it holds the set of pages, tracks the
current one, and drives the toolbar (clock, Live pill, Refresh, Export).

The **navigation bar** (`src/Shell/Navigation`) is a self-contained, **dockable and collapsible**
component. Its view-model owns orientation and collapsed state and exposes *every* derived layout value
— dock edge, rail thickness, item axis, label/brand visibility, accent-indicator orientation, scroll
axis, collapse-puck geometry — as **computed properties, with no value converters**. The bar carries no
permanent control chrome; four entry points drive the *same* shared view-model:

- a **hover-revealed chevron puck** straddling the bar's outer edge, which collapses and expands it;
- **right-clicking anywhere on the bar**, which opens a dock menu at the pointer;
- **dragging the brand area** to a window edge, which dims the bar in place while a drop band and a
  floating chip show the target;
- the **Navigation** group in Settings → Appearance.

Dock edge and collapsed state are persisted and restored on the next launch (see *Settings persistence*
below).

`ViewLocator` maps a `*ViewModel` to its `*View` by type name, so a tab's view and view-model must
share a namespace.

## Page lifecycle: always-on pages and marker interfaces

Data-bearing tabs (Dashboard, Network, Processes, Performance, Storage) are **always-on singletons**: their
view-models are constructed once by the shell and live for the app's lifetime, so their timers and
rolling buffers keep running as you switch tabs. Rather than a common base class dictating behaviour, pages opt into shell
behaviours by implementing small **marker interfaces** in `src/Shared`:

- **`ISelfScrollingPage`** — the page fills the viewport and manages its own internal scrolling, so the
  shell must *not* wrap it in a page-level scroll region. The page host is a panel with two
  mutually-exclusive content hosts (a scrolling `ScrollViewer` and a bounded `ContentControl`); the
  current page is routed to whichever matches, so its view is only built once. File Explorer uses this
  to give each of its three panes an independent scrollbar; Processes and Performance use it for their
  own bounded, internally-scrolling layouts. Network and Storage deliberately page-scroll instead.
- **`IRefreshablePage`** — the toolbar **Refresh** button routes to `Refresh()`. The Dashboard
  re-samples every metric; File Explorer reloads the current folder; pages that don't implement it
  simply ignore Refresh. Every data-bearing tab implements it, Hardware included.
- **`ILiveSamplingPage`** — the toolbar **Live** pill pauses/resumes sampling. `MainWindowViewModel`
  routes a single toggle across every page that implements the interface, so one control governs all
  live sampling at once. Hardware is the one data tab that opts out: it reads static facts, so there is
  nothing to pause.

This keeps the shell decoupled from any specific tab: it reasons about capabilities
("is the current page refreshable?"), never concrete types.

## Live data: samplers and providers

Live and static machine data follow two complementary patterns, both of which **soft-fail**.

**Samplers** produce a fresh value on a timer. The reference example is the Dashboard's metric set: each
metric has its own 1 Hz `DispatcherTimer` and a 60-sample rolling buffer in the view-model, fed by a
feature-local sampler (typically Win32 P/Invoke or a managed counter). Samplers that more than one tab
needs live under `src/Services` — e.g. the CPU and Memory samplers (`src/Services/SystemMetrics`) and
the network throughput sampler (`src/Services/Network`), each promoted there when a second tab needed
the same reading.

**Providers** read *static* facts once, off the UI thread. The idiom (see `SystemInfoProvider`) is:

```csharp
public static Task<TInfo> GetAsync() => Task.Run(Read);

private static TInfo Read() {
    if (!OperatingSystem.IsWindows())   // doubles as the platform-compat guard
        return TInfo.Unknown;
    try { /* WMI / registry / P/Invoke */ }
    catch { return TInfo.Unknown; }     // each section also falls back independently
}
```

Two conventions worth knowing:

- **Soft-fail granularity.** A provider returns an `Unknown` snapshot on total failure, but each field
  or section also falls back on its own, so a single dead source ("Unknown BIOS") never blanks the rest
  of the panel.
- **The network sampler samples one primary adapter, never a sum.** On modern .NET,
  `NetworkInterface.GetAllNetworkInterfaces()` returns many virtual/filter adapters that *mirror* the
  physical NIC's byte counters, so summing them multi-counts the same traffic. The sampler instead
  selects the internet-facing adapter (up, non-loopback, has a default gateway, busiest by bytes) and
  locks to it across ticks — matching Task Manager's per-adapter numbers. `SelectPrimary()` is the one
  source of truth for "which adapter is primary", reused by the Network tab's IP-config panel.

## Theming

Colours live in `src/Shared/Styles/Palette.axaml` in three groups:

1. **Theme-variant keys** (surfaces, lines, the text ramp, hover overlays) sit in
   `ResourceDictionary.ThemeDictionaries` under `Dark`/`Light` and flip automatically with the app's
   `ThemeVariant`.
2. **The accent set** (`Accent`, `AccentHover`, `OnAccent`, `AccentSoft`, …) sits top-level and is
   swapped at runtime.
3. **Per-graph chart-series keys** (`ChartCpu`, `ChartMemory`, `ChartGpu`, …) also sit top-level and are
   swapped at runtime.

**`ThemeService`** (`src/Services/Theming`) is the **single seam** — the only code that writes to
`Application.Current`. It applies the theme variant, swaps the accent (and recolours every chart key to
match), or restores the default multi-colour look (each graph its own colour, highlight blue). It is
constructed once in `MainWindowViewModel`, applied at startup, and handed to `SettingsViewModel`.

**The one rule:** any resource key that can change at runtime must be referenced with
`{DynamicResource …}`, never `{StaticResource}`. Only the fixed legend colours
(`Blue`/`Green`/`Purple`/`Orange`/`Yellow`) stay static. The chosen theme and accent are persisted and
reapplied at launch (see *Settings persistence* below).

## Settings persistence

User choices are persisted as JSON at `%AppData%/DashDetective/settings.json` by **`SettingsStore`**
(`src/Services/Settings`). **`AppSettings`** is an immutable record holding the whole of that state —
theme, accent name, nav orientation/collapse, refresh interval, show-hidden-files, launch-at-startup,
tray, resource alerts, and the Performance tab's view toggles. The composition root applies it on load
and captures a fresh snapshot to save whenever a control changes. `SettingsJsonContext` is a
source-generated `System.Text.Json` context, so serialization stays reflection-free and trim-friendly.

Two conventions keep a bad file from being fatal:

- **Every property has a default**, so a file written by an older schema — or missing fields after a
  hand-edit — still deserializes rather than failing.
- **A `SchemaVersion` guards incompatible changes**; a mismatch falls back to `AppSettings.Defaults`.
  Combined with the store's soft-fail read (a missing, unreadable or corrupt file logs a warning and
  returns defaults), a broken settings file can never prevent launch.

`SettingsStore` has an `internal` constructor taking an explicit file path — production resolves
`%AppData%` — which is how the persistence tests run against a temporary file.

## Shared control inventory

Reusable widgets live in `src/Shared/Controls`:

- **`Sparkline`** — a compact line chart. Auto-fits to its data by default, or takes a fixed `YMin`/`YMax`
  axis (used for the 0–100 utilization charts). Fixed-axis mode also supports an optional second series
  plus gradient area fill, which the Network throughput panel uses to plot download and upload on one
  scale.
- **`StatCard`** — a headline metric card wrapping a `Sparkline` (it forwards `YMin`/`YMax` through).
- **`InfoRow`** — a key/value row; long values wrap flush-right onto multiple lines instead of clipping,
  so verbose vendor strings (e.g. a full BIOS manufacturer name) are shown in full.
- **`ExpandablePathRow`** — an `InfoRow` variant for long filesystem paths, which expands to show the
  full value rather than truncating it.

The geometry behind the charts is kept out of the controls in `src/Shared/Charts`, as pure static
helpers: **`ChartScale`** resolves the Y axis (auto-fit or fixed `YMin`/`YMax`) and **`SparklinePoints`**
projects samples to points. Keeping them free of Avalonia types is what lets them be unit-tested
directly, without a headless render pass.

Shared styles (card, panel, segmented control, toggle, buttons, the draggable `paneSplitter`, …) live
in `src/Shared/Styles/SharedStyles.axaml`. Controls or styles used by only one tab stay tab-local until
a second tab needs them (the Network tab's console colours and File Explorer's checkbox style are
current examples).

## Dependencies

Beyond Avalonia (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`) and
`CommunityToolkit.Mvvm`, the only added runtime package is **`System.Management`** (WMI).
`AvaloniaUI.DiagnosticsSupport` is referenced for the Debug configuration only and is excluded from
Release builds. Everything else is in-box: `System.Net.NetworkInformation` / `Ping` / `Dns`,
`Microsoft.Win32.Registry`, `System.Text.Json`, `System.Diagnostics.Process`, and PDH performance
counters. Adding a new package is a deliberate, signed-off decision.

Native access is all hand-written P/Invoke against DLLs already present on the machine — nothing is
referenced, redistributed or shipped, and none of it needs admin rights:

| Library | Used for |
| --- | --- |
| `pdh.dll` | Performance-counter queries (GPU, disk, per-process metrics) |
| `iphlpapi.dll` | The TCP/UDP connections table with owning PIDs |
| `shell32.dll` | Shell integration — Open and the native Properties sheet |
| `kernel32.dll`, `psapi.dll` | Process I/O counters and memory reads; the NVMe health-log IOCTL |
| `dxgi.dll` | Enumerating physical GPU adapters by LUID |
| `user32.dll`, `dwmapi.dll` | Window and title-bar integration |
| `nvapi64.dll`, `nvml.dll`, `atiadlxx.dll` | GPU temperature and power, installed by the display driver |

The three vendor GPU libraries are the reason a unified sensor library is not needed. If a machine
lacks a given vendor's driver, the `DllImport` simply fails and that tile degrades to "—" on its own.

## Testing

Unit tests live in `tests/DashDetective.Tests` (xUnit, also targeting `net10.0-windows` because it
references the Windows-only app assembly). Fakes are small hand-written classes under `Fakes/` — there
is no mocking framework, matching the codebase's zero-dependency ethos. The test layout mirrors the app,
so a test file sits at the same relative path as its subject
(`src/Shared/Charts/SparklinePoints.cs` → `tests/DashDetective.Tests/Shared/Charts/SparklinePointsTests.cs`).

The architecture is shaped to make this possible headlessly, without an Avalonia dispatcher or real
hardware. Two seams do most of that work:

- **`IUiTimer` + `DispatcherTimerAdapter`** (`src/Services/Threading`) abstract the UI-thread timer, so
  `MetricChannel` and `SystemMetricsService` can be stepped deterministically by `FakeUiTimer` instead of
  waiting on wall-clock ticks. Production still uses a real `DispatcherTimer` by default.
- **`InternalsVisibleTo("DashDetective.Tests")`** (in the app csproj) exposes a small number of
  `internal` constructors and widened members — `SystemMetricsService`'s sampler-bundle ctor,
  `SettingsStore`'s explicit-path ctor — so the hardware samplers and the settings file can be faked.
  These seams are additive: they never change production behaviour.

The consequence for new code is a convention: **pure logic belongs outside the view-models.** Formatters,
catalogs, chart maths and paging maths are extracted as static helpers in `src/Shared` or the tab folder
precisely so they can be tested directly, which is why `ChartScale`, `PagerMath`, `ProcessTreeBuilder`
and the formatter family exist as separate types.

## Quality gates

- **`.editorconfig`** (repo root) encodes the house style: four-space indent, file-scoped namespaces,
  K&R braces, broad `var` usage. Usings are sorted alphabetically, with `System` **not** first.
- The build sets **`TreatWarningsAsErrors`**, **`EnforceCodeStyleInBuild`** and **`AnalysisLevel=latest`**
  (platform-compatibility analyzers), so style and platform issues fail the build.
- **CI** (`.github/workflows/dotnet-desktop.yml`) runs on `windows-latest` over a Debug/Release matrix:
  `dotnet format --verify-no-changes` runs before building, so unformatted code fails fast, then the test
  suite runs with Cobertura coverage collected via coverlet.
