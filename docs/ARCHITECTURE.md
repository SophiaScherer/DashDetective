# DashDetective — Architecture

This document explains how DashDetective is put together: the shell, how pages are hosted and kept
alive, the sampler/provider conventions behind the live data, the theming seam, and the inventory of
shared controls. It is a reader-facing distillation of the project's internal working notes — enough to
find your way around the code without reading every file.

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

Source lives under `DashDetective/src/`, split into three areas. Namespaces follow folders
(`DashDetective.Shared`, `DashDetective.Services.Theming`, `DashDetective.Shell`,
`DashDetective.Tabs.<Feature>`, …).

| Area | Holds |
| --- | --- |
| `src/Shared` | Cross-cutting, feature-agnostic building blocks: `ViewModelBase`, the marker interfaces, `AppInfo`, reusable controls, styles and the colour palette. |
| `src/Services` | Cross-cutting services shared by more than one tab: `Theming` (the `ThemeService` seam), `SystemMetrics` (CPU/Memory/GPU/Storage samplers), and the shared `Network` throughput sampler. |
| `src/Shell` | The application frame: `MainWindow`, `MainWindowViewModel`, `ViewLocator`, and the dockable `Navigation` bar. |
| `src/Tabs/<Feature>` | One folder per tab (Dashboard, FileExplorer, Processes, Performance, Network, Hardware, Settings). |

**Rule of thumb:** anything reused by more than one tab (a control, a colour, a sampler) belongs in
`Shared`/`Services`; everything else stays inside its tab folder.

## The shell and navigation

`MainWindow`'s root is a `DockPanel` that hosts the navigation bar at the user-chosen edge, plus the
main content area. `MainWindowViewModel` owns page routing: it holds the set of pages, tracks the
current one, and drives the toolbar (clock, Live pill, Refresh, Export).

The **navigation bar** (`src/Shell/Navigation`) is a self-contained, **dockable and collapsible**
component. Its view-model owns orientation and collapsed state and exposes *every* derived layout value
— dock edge, rail thickness, item axis, label/brand visibility, accent-indicator orientation, scroll
axis — as **computed properties, with no value converters**. Two entry points drive the *same* shared
view-model: on-bar controls (a collapse chevron and a kebab menu offering the four dock positions), and
the **Navigation** group in Settings → Appearance. The bar can also be **dragged to an edge to dock**,
with a floating hint chip showing the target. Navigation state is session-only (resets to left/expanded
each launch).

`ViewLocator` maps a `*ViewModel` to its `*View` by type name, so a tab's view and view-model must
share a namespace.

## Page lifecycle: always-on pages and marker interfaces

Data-bearing tabs (Dashboard, Network, Processes, Performance) are **always-on singletons**: their
view-models are constructed once by the shell and live for the app's lifetime, so their timers and
rolling buffers keep running as you switch tabs. Rather than a common base class dictating behaviour, pages opt into shell
behaviours by implementing small **marker interfaces** in `src/Shared`:

- **`ISelfScrollingPage`** — the page fills the viewport and manages its own internal scrolling, so the
  shell must *not* wrap it in a page-level scroll region. The page host is a panel with two
  mutually-exclusive content hosts (a scrolling `ScrollViewer` and a bounded `ContentControl`); the
  current page is routed to whichever matches, so its view is only built once. File Explorer uses this
  to give each of its three panes an independent scrollbar.
- **`IRefreshablePage`** — the toolbar **Refresh** button routes to `Refresh()`. The Dashboard
  re-samples every metric; File Explorer reloads the current folder; pages that don't implement it
  simply ignore Refresh.
- **`ILiveSamplingPage`** — the toolbar **Live** pill pauses/resumes sampling. `MainWindowViewModel`
  routes a single toggle across every page that implements the interface, so one control governs all
  live sampling at once.

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
(`Blue`/`Green`/`Purple`/`Orange`/`Yellow`) stay static. Theming is session-only by choice (no
persistence yet — see the Roadmap).

## Shared control inventory

Reusable widgets live in `src/Shared/Controls`:

- **`Sparkline`** — a compact line chart. Auto-fits to its data by default, or takes a fixed `YMin`/`YMax`
  axis (used for the 0–100 utilization charts). Fixed-axis mode also supports an optional second series
  plus gradient area fill, which the Network throughput panel uses to plot download and upload on one
  scale.
- **`StatCard`** — a headline metric card wrapping a `Sparkline` (it forwards `YMin`/`YMax` through).
- **`InfoRow`** — a key/value row; long values wrap flush-right onto multiple lines instead of clipping,
  so verbose vendor strings (e.g. a full BIOS manufacturer name) are shown in full.

Shared styles (card, panel, segmented control, toggle, buttons, the draggable `paneSplitter`, …) live
in `src/Shared/Styles/SharedStyles.axaml`. Controls or styles used by only one tab stay tab-local until
a second tab needs them (the Network tab's console colours and File Explorer's checkbox style are
current examples).

## Dependencies

Beyond Avalonia and `CommunityToolkit.Mvvm`, the only added package is **`System.Management`** (WMI).
Everything else is in-box: `System.Net.NetworkInformation` / `Ping` / `Dns`, `Microsoft.Win32.Registry`,
`System.Diagnostics.Process`, PDH performance counters, and hand-written P/Invoke (`shell32`,
`iphlpapi`, and process/GPU counters). Adding a new package is a deliberate, signed-off decision.

## Quality gates

- **`.editorconfig`** (repo root) encodes the house style: four-space indent, file-scoped namespaces,
  K&R braces, broad `var` usage.
- The build sets **`TreatWarningsAsErrors`**, **`EnforceCodeStyleInBuild`** and **`AnalysisLevel=latest`**
  (platform-compatibility analyzers), so style and platform issues fail the build.
- **CI** runs `dotnet format --verify-no-changes` before building, so unformatted code fails fast.
