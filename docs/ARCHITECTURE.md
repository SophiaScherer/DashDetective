# DashDetective — Architecture

A high-level map of how the app is put together: the layers, the seams between them, and where things
live. Start here, then go deeper as needed:

| | |
| --- | --- |
| [SOURCE-MAP.md](SOURCE-MAP.md) | Every file under `src/`: what it is for, and the trap it avoids. |
| [FEATURES.md](FEATURES.md) | What each shipped feature does, and the decisions inside it. |
| [AGENTS.md](../AGENTS.md) | The rules a change has to satisfy. |
| [README](../README.md) | Building, running and testing. |

## What it is

An [Avalonia UI](https://avaloniaui.net/) desktop app on `net10.0`, MVVM via `CommunityToolkit.Mvvm`.
It reads the local machine and renders it as nine tabs. Data sources are Windows-native (WMI, PDH,
registry, Win32 P/Invoke); Linux reads the equivalent pseudo-files under `/proc` and `/sys`.

**One neutral `net10.0` TFM for both projects.** No multi-targeting, no `#if`, no per-platform project
split — the platform is chosen at runtime in one place per seam, a provider's `ForCurrentPlatform()`.
That neutral TFM is also what makes the CA1416 platform-compatibility analyzer a real build gate.

## Codebase layout

```
DashDetective/
  App.axaml, Program.cs, app.manifest, Assets/
  src/
    Shared/          cross-cutting, feature-agnostic
      Controls/      Sparkline, StatCard, WidgetPanel, InfoRow, SearchField, …
      Layout/        WidgetBoard, UniformFlowPanel, GridColumns, and their pure arithmetic
      Charts/        MetricHistory, ChartScale, ChartAxis, SparklinePoints, …
      Styles/        Palette (colour), Dimensions (layout), SharedStyles, Widgets, Layout
      Shortcuts/     ShortcutCatalog and the shortcut model
      Completion/    PrefixCompleter, shared by every field that ghosts a suggestion
      (root)         ViewModelBase, marker interfaces, formatters, Placeholders, OverlapGuard
    Services/        shared by more than one tab
      SystemMetrics/ CPU, memory, GPU, storage samplers and providers
      Platform/      Linux (IProcFileSystem + the /proc and /sys parsers), Windows (WmiRead)
      Theming/       ThemeService, ChartPalette, AccentPreset
      Settings/      AppSettings, SettingsStore, SettingsJsonContext
      Network/ Startup/ Threading/ Identity/ Diagnostics/ Search/
    Shell/           MainWindow, MainWindowViewModel, ViewLocator, Navigation, Search, Help, Shortcuts
    Tabs/<Feature>/  one folder per tab: view, view-model, feature-local helpers
tests/DashDetective.Tests/   mirrors src/ path for path
docs/                        this file
```

Namespaces follow folders. Anything a second tab needs moves to `Shared`/`Services`; everything else
stays in its tab folder.

## The shell

`MainWindow` is a `DockPanel`: the navigation bar at the user-chosen edge, then a toolbar and the page
host. `MainWindowViewModel` owns page routing, the toolbar (clock, Live pill, Refresh, Export) and the
composition root for services.

`ViewLocator` maps a `*ViewModel` to its `*View` through an explicit switch — no reflection — so a
tab's view and view-model must share a namespace.

The **navigation bar** (`src/Shell/Navigation`) is self-contained, dockable and collapsible. Its
view-model exposes every derived layout value (dock edge, rail thickness, item axis, label visibility,
puck geometry) as computed properties, with no value converters. Four entry points drive it: a
hover-revealed chevron puck, a right-click dock menu, dragging the brand area to a window edge, and
Settings → Appearance.

**Universal search** (`src/Shell/Search`) fans out across providers — pages, settings, processes,
files — with no routing layer; each page exposes a `Reveal()` seam for jumping to a result. It sits in
the toolbar as an icon until asked for, by the icon itself or Ctrl+F, and puts itself away again when
it loses focus with nothing typed. That state is deliberately not persisted.

## Page lifecycle

Data-bearing tabs are long-lived singletons, constructed once and kept for the app's lifetime so their
rolling buffers survive a tab switch. Their **timers** are not: a page samples only while it is the
visible tab in a visible window.

There is no page base class. Pages opt into shell behaviours with marker interfaces in `src/Shared`:

| Interface | The shell does |
| --- | --- |
| `ISelfScrollingPage` | Skips the page-level scroll region; the page scrolls its own panes |
| `IRefreshablePage` | Routes the toolbar Refresh to `Refresh()` |
| `ILiveSamplingPage` | Routes the toolbar Live pill to `SetLive()` |
| `IActivatablePage` | Calls `SetActive()` as the page comes on and off screen |
| `IShortcutTarget` | Offers the page keyboard shortcuts in its own `ShortcutScope` |

Each page composes the last two with a **`SamplingGate`**: sampling runs when the pill is on *and* the
page is on screen. The gate also owns a `CancellationTokenSource` for the work it starts, so a read
still in flight cannot land on a page nobody is looking at. A deactivated page drops its
`SystemMetricsService` subscriptions, which are ref-counted; re-attaching replays each feed's cached
sample so the page seeds with real data.

Overlapping polls are refused rather than queued, via `OverlapGuard` (`src/Shared`). Three other
overlap idioms exist on purpose and are not interchangeable — a generation counter for File Explorer's
user-driven folder loads, `AllowConcurrentExecutions = false` for Toolkit's run command, and a real
lock for `NvidiaSmiReader`, which is called off the UI thread.

## Widgets and page layout

A widget is a **`WidgetPanel`** (`src/Shared/Controls`): the panel surface, a header row, and the body
the call site supplies. It is a `ContentControl` templated from `src/Shared/Styles/Widgets.axaml`, with
`Title`, `Subtitle`, `HeaderLead` (content against the title), `HeaderContent` (content at the far end)
and `WidgetId` — a `{page}.{slug}` identity a saved layout can name it by. A `Border Classes="panel"`
that survives is a surface, not a widget; the header is the distinction.

A page's widgets are children of one **`WidgetBoard`** (`src/Shared/Layout`), which packs them into
rows that fit the window. Each declares its own `MinWidth` and an attached `WidgetBoard.MaxSlotWidth`,
and **a row keeps pulling the next widget in while it is still too roomy** — so surplus width buys
another column rather than a wider widget. Widgets are dragged by their headers to reorder, which
re-packs live. The arithmetic is `WidgetBoardLayout`, free of Avalonia types and unit-tested without a
layout pass.

The order is persisted per page by `WidgetOrders`, keyed by widget id rather than index. Resolving a
saved order against what a page declares now drops ids it no longer has and keeps a newly added widget
at its declared position, so a later release does not drop a new widget at the bottom of a layout the
user arranged once.

**`WidgetTable`** (`src/Shared/Controls`) is the chrome a table inside a widget needs: a header that
stays above a body that scrolls, with the scrollbar gutter applied to both so the columns cannot drift.
Columns, sorting and the row template stay with the call site. Only Network's connections table and
Storage's partitions use it — File Explorer measures its column drops off its own header grid's width,
Processes scrolls sideways rather than dropping columns at all, and they share little else.

Other layout panels: `UniformFlowPanel` (equal columns that wrap rather than shrink) and
`GridColumns`/`TableColumns` (bindable column definitions, and dropping columns as a table narrows).

## Charts

There is no charting library. **`Sparkline`** (`src/Shared/Controls`) draws every chart, either
auto-fitting its data or on a fixed `YMin`/`YMax` axis with an optional second series, area fill, grid
and axis furniture. Chart shape is picked by style class (`chartHero`, `chartPanel`, `chartCell`,
`chartMini` in `Styles/Layout.axaml`), never by an inline height.

Geometry and wording live outside the control in `src/Shared/Charts` — `MetricHistory` (the rolling
buffer and how much of it is real), `ChartScale`, `SparklinePoints`, `ChartAxis`, `ChartWindow`,
`ChartStatus`. Text is *measured* by the control, which alone has the typeface, and *composed* by these
helpers, which is what makes them testable without a render pass.

## Live data

**Samplers** produce a fresh value on a timer; **providers** read static facts once, off the UI thread.
Both soft-fail. Samplers a second tab needs live in `src/Services`; the rest stay tab-local.

`SystemMetricsService` owns the shared feeds and hands them out as ref-counted subscriptions, so two
tabs watching CPU cause one poll. `MetricChannel` wraps a sampler with its failure state, which is what
turns a throw into the on-screen placeholder. It is **pure fan-out**: GPU and disk have no shared feed
there, because an aggregate across several devices would report an average under a label naming one of
them.

`ResourceAlertWatcher` lives outside it for that reason. It subscribes to the shared CPU and memory
feeds like any page, but owns its own GPU and disk samplers — which their contracts require, both being
stateful — and never aggregates: it takes the **worst device and names it** in the banner. Its sustain
window is seconds converted against the live interval, not a sample count, so the wait means the same
thing at every refresh cadence.

On Linux every reader goes through **`IProcFileSystem`** (`src/Services/Platform/Linux`) rather than
`System.IO`, and each pseudo-file has one parser named after it (`ProcStatParser`,
`ProcMeminfoParser`, `ProcPidStatParser`, …). Derived facts shared by several surfaces — `CpuFacts`,
`SysBlockFacts`, `DrmCardFacts`, `ProcPids`, `ProcPidName` — sit on those parsers so two tabs cannot
report different numbers for the same machine.

## Theming

Colour lives in `src/Shared/Styles/Palette.axaml` in three groups: theme-variant keys (surfaces, lines,
the text ramp) under `ThemeDictionaries`; the accent set; and the chart-series keys. The last two are
rewritten at runtime, so **anything that can change at runtime is bound with `{DynamicResource}`**.

**`ThemeService`** (`src/Services/Theming`) is the only code that writes to `Application.Current`. An
accent change re-hues the chart palette through `ChartPalette.Derive` rather than flattening all six
series to one colour, so per-metric colour coding survives any accent.

Layout dimensions live separately in `Dimensions.axaml` and are theme-invariant, so they are always
`{StaticResource}`.

## Settings

`AppSettings` is an immutable record of the whole persisted state; `SettingsStore`
(`src/Services/Settings`) writes it as JSON to `%AppData%/DashDetective/settings.json` (on Linux,
`$XDG_CONFIG_HOME` ?? `~/.config`), debounced and written atomically. `SettingsJsonContext` is
source-generated, so serialization stays reflection-free.

Every property has a default and a `SchemaVersion` guards incompatible changes, so a missing, corrupt
or older file falls back to defaults rather than preventing launch. Collection-shaped state is stored
as one encoded string, because the record's value equality — which the save round-trip relies on —
compares collections by reference.

**`Load` merges the file over `AppSettings.Defaults` key by key, and that is what makes the sentence
above true.** The source generator treats a record's `init` properties as constructor parameters and
fills the slots a file omits with `default(T)`, so deserializing directly discards every non-default
initializer for a property the file predates. Merging as JSON keeps the fix general: a property added
later inherits its default with no further work.

`MainWindowViewModel` is the only place settings are applied and captured: `ApplySettings` pushes each
value into the seam that owns it, `CaptureCurrent` reads them back.

## Diagnostics export

The system report is **data, not text**: `DiagnosticsReport` (`src/Services/Diagnostics`) is sections of
key/value rows, composed by `MainWindowViewModel.BuildReportModel` from the Dashboard's own sections
plus read-only row accessors on Hardware, Network and Storage. Each format — text, JSON, Markdown, HTML,
CSV — is a renderer over that one model, so the pages keep supplying plain tuples and know nothing about
export. It was string concatenation split across the shell and the Dashboard before, which is why there
was only ever one format.

`DiagnosticsFormats` is the single table of what each format is called and saves as, and the one place a
renderer is chosen. `FileSave` (`src/Shared`) owns the save dialog for every export, and reads the format
back off the **chosen filename** rather than the picked filter, which Avalonia does not report. The text
renderer is pinned byte for byte by a test: a saved report and a new one have to diff cleanly.

## Keyboard shortcuts

`ShortcutCatalog` (`src/Shared/Shortcuts`) is the shipped default table: gesture, scope, whether it
survives a focused text box, and the Help copy. `ShortcutBindings` is what is actually in force — those
defaults with the user's rebinds applied. One instance is shared by the key handler, the Help modal,
universal search and the Settings Keyboard card, so a live binding cannot go undocumented and all four
describe the same thing. Resolution and Help grouping live on the instance; the catalog keeps the pure
table-taking helpers, so neither exists twice.

A rebind replaces **all** of a shortcut's default gestures, and is refused when the gesture is already
taken **within the same scope** — cross-scope duplicates stay legal, since only one tab is ever current.
Overrides persist as one encoded string, ids and keys by name.

`ShellShortcutHandler` (`src/Shell/Shortcuts`) attaches one tunneling `KeyDown` handler to the window.
Dispatch is a priority chain on `MainWindowViewModel.HandleShortcut` — a capture in progress, then an
open modal, then the search dropdown, then the current page's scope, then global — so it is testable
without a UI. **The capture check has to come first**: the handler tunnels from the window and so sees
a press before the Settings capture box does, and would otherwise run the shortcut being rebound.

## Cross-platform seams

Per-platform behaviour is resolved at runtime behind an interface, one place per concern:

| Seam | Windows | Linux |
| --- | --- | --- |
| `IFileSystemRoots` | Drive letters | `/`, `$HOME`, removable mounts from `/proc/mounts` |
| `IStartupRegistration` | HKCU `Run` value | XDG `.desktop` in `~/.config/autostart` |
| `IUserPictureProvider` | `AccountPicture\Users\{SID}` index, then `%PUBLIC%\AccountPictures` | `~/.face`, `~/.face.icon`, AccountsService icon |
| `IShellInterop` / `IProcessInterop` | Shell type names, Properties sheet | Reveal the containing folder |
| `IToolkitCatalog` | Windows command set | Linux command set |
| `IProcFileSystem` | — | `/proc` and `/sys` reads |

Two statics in `src/Shared` hold the rest: `PathComparison` (case-sensitivity of path *identity*) and
`SystemDrive` (`Letter` for keying volume records, `Root` for opening or measuring). Environment
expansion belongs to `ToolkitPaths.Resolve`, never a direct `ExpandEnvironmentVariables` call.
`TrayIntegration` gates hide-to-tray to Windows, since stock GNOME runs no StatusNotifierItem host.

## Dependencies

Avalonia (`Avalonia`, `.Desktop`, `.Themes.Fluent`, `.Fonts.Inter`), `CommunityToolkit.Mvvm`,
`System.Management` (WMI) and `System.Data.OleDb` (the Windows Search index).
`AvaloniaUI.DiagnosticsSupport` is Debug-only. Everything else is in-box. Adding a package is a
deliberate, signed-off decision.

Native access is hand-written P/Invoke against DLLs already on the machine — nothing is redistributed
and none of it needs admin rights:

| Library | Used for |
| --- | --- |
| `pdh.dll` | Performance counters (GPU, disk, per-process) |
| `iphlpapi.dll` | TCP/UDP connection table with owning PIDs |
| `shell32.dll` | Open and the native Properties sheet |
| `kernel32.dll`, `psapi.dll` | Process I/O and memory; the NVMe health-log IOCTL |
| `dxgi.dll` | Enumerating GPU adapters by LUID |
| `user32.dll`, `dwmapi.dll` | Window and title-bar integration |
| `nvapi64.dll`, `nvml.dll`, `atiadlxx.dll` | GPU temperature and power, from the display driver |

Linux needs none of it: the kernel publishes the same readings as pseudo-files. The one exception is
NVIDIA utilisation, which needs an `nvidia-smi` process launch and is therefore the app's only opt-in
reading — off by default, rate-limited, and never on the sampling path.

## Testing

xUnit, in `tests/DashDetective.Tests`, on the same neutral TFM. Test files mirror the source path.
Fakes are small hand-written classes under `Fakes/` — no mocking framework.

Two seams make headless testing possible without a dispatcher or real hardware:

- **`IUiTimer` + `DispatcherTimerAdapter`** (`src/Services/Threading`) abstract the UI-thread timer, so
  `MetricChannel` and `SystemMetricsService` can be stepped deterministically by `FakeUiTimer`.
- **`InternalsVisibleTo("DashDetective.Tests")`** exposes a few `internal` injection constructors —
  `SystemMetricsService`'s sampler bundle, `SettingsStore`'s explicit path, `NetworkViewModel`'s
  seeding task. These are additive and never change production behaviour.

The convention that follows: **pure logic belongs outside the view-models.** Formatters, catalogs,
chart maths, paging and layout arithmetic are extracted as static helpers precisely so they can be
tested directly — which is why `ChartScale`, `PagerMath`, `WidgetBoardLayout`, `FlowLayout` and the
formatter family exist as separate types.
