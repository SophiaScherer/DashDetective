# DashDetective — Architecture

This document explains how DashDetective is put together: the shell, how pages are hosted and kept
alive, the sampler/provider conventions behind the live data, the theming seam, settings persistence,
the shared control inventory, and the seams that make the whole thing testable. It is a reader-facing
distillation of the project's internal working notes — enough to find your way around the code without
reading every file.

Build, run and test instructions live in the [README](../README.md).

DashDetective is an [Avalonia UI](https://avaloniaui.net/) desktop app on `net10.0`, using the MVVM
pattern with `CommunityToolkit.Mvvm`. Its data sources are Windows-native (WMI, PDH performance
counters, registry, and Win32 P/Invoke), and a Linux port is being rolled out one milestone at a time —
today Linux builds and launches with CPU, memory, the whole Network tab — throughput, adapters and the
active TCP/UDP connections with their owning processes — the whole Storage surface — drives, partitions
and per-disk activity — the Processes tab, the Toolkit tab's own command set, the desktop integration
(File Explorer roots, "Launch at startup", friendly file types), plus the machine's static identity
(OS, kernel, BIOS, board) and the Processor, Motherboard and Storage Devices spec cards, while the
remaining panels read "—".

**Both projects target a single neutral `net10.0` TFM.** There is no multi-targeting, no `#if`, and no
per-platform project split: the platform is decided **at runtime**, in exactly one place per seam — the
provider's `ForCurrentPlatform()`. A neutral TFM is also what makes the platform-compatibility analyzer
(CA1416) a real gate, so a Windows-only API reached from unguarded code is a build error rather than a
crash on someone else's machine. Windows-only NuGet assets still load correctly because packages like
`System.Management` ship `runtimes/win` alongside their neutral stub, and the host resolves by RID.

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
| `src/Shared` | Cross-cutting, feature-agnostic building blocks: `ViewModelBase`, the marker interfaces, `AppInfo`, reusable controls, styles and the colour palette, the `Shortcuts` model (`ShortcutCatalog` and friends), the pure-logic `Charts` helpers (`ChartScale`, `SparklinePoints`) and formatters (`DataRateFormatter`, `UptimeFormatter`, `HardwareNameFormatter`, `CollectionReconciler`). |
| `src/Services` | Cross-cutting services shared by more than one tab: `Theming` (the `ThemeService` seam), `SystemMetrics` (CPU/Memory/GPU/Storage samplers and providers), `Network` (the shared throughput sampler), `Settings` (the persistence store), `Startup` (launch-at-startup registration), `Threading` (the `IUiTimer` seam), `Identity` and `Diagnostics`. |
| `src/Shell` | The application frame: `MainWindow`, `MainWindowViewModel`, `ViewLocator`, the dockable `Navigation` bar, the `Help` modal and the `Shortcuts` key listener. |
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

- a **hover-revealed chevron puck**, a half-disc standing just off the bar's outer edge, which collapses
  and expands it;
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
- **`IShortcutTarget`** — the page handles keyboard shortcuts of its own, and names the
  `ShortcutScope` its bindings belong to. Processes, File Explorer and Network implement it; see
  *Keyboard shortcuts* below.

This keeps the shell decoupled from any specific tab: it reasons about capabilities
("is the current page refreshable?"), never concrete types.

## Keyboard shortcuts

`src/Shared/Shortcuts` holds the whole model; `src/Shell/Shortcuts` holds the listener.

**`ShortcutCatalog` is the single source of truth.** One static table maps each `ShortcutId` to its
gestures, its scope, whether it survives a focused text box, and the copy the Help modal shows. The key
handler resolves against it *and* the Help modal renders from it, so a live binding cannot go
undocumented, nor a documented one go dead. It holds no control types, so it is fully unit-testable.

**One listener, not `Window.KeyBindings`.** `ShellShortcutHandler` attaches a single tunneling
`KeyDown` handler to the window — the idiom the Help overlay and File Explorer already use for
window-wide input. Tunneling is required: it reaches the shell before Avalonia's focus manager claims
`Ctrl+Tab` as tab-group navigation. It also lets bare keys (`Delete`, `Backspace`, `/`, `Enter`) be
suppressed while a `TextBox` has focus, via the `AllowInTextInput` flag.

**Scoped resolution.** A gesture may mean different things on different tabs, because only one tab is
ever current — `Alt+↑` sorts on Processes and climbs a folder on File Explorer. Resolution tries the
active page's `ShortcutScope` first and falls back to `Global`.

**Dispatch is a priority chain**, and lives on `MainWindowViewModel.HandleShortcut` rather than in the
window, so it is testable without a UI:

1. **An open modal owns the keyboard.** While the Help modal is up, `Esc` closes it and every other
   shortcut is swallowed, so a key can never act on the page behind the scrim. Pages apply the same
   rule one level down for their own overlays (the Processes end-task confirmation, the File Explorer
   path box).
2. **The current page**, if it implements `IShortcutTarget`.
3. **Global** handling.

A handler returns whether it actually did something, and the listener sets `e.Handled` from that. A
shortcut with nothing to act on — `Delete` with no selection, `Esc` with no banner showing — therefore
falls through to the rest of the app instead of being silently eaten.

**`Esc` has exactly one owner.** It is resolved through this chain, not by individual controls, so its
context-sensitivity is decided in one readable place.

Actions the view model cannot run itself are raised as events for the window to service — `Export`
needs the window's `StorageProvider` for its file picker, so it goes out through
`MainWindowViewModel.ExportRequested`. Routing it through the chain rather than letting the window
short-circuit is what keeps it subject to the modal rule above. Focus requests (`Ctrl+F`, `Ctrl+L`, `/`)
use the same view-model-event seam.

The universal-search dropdown sits between the modal and the page in that chain: while it is open the
shell reports `ShortcutScope.Search`, so the bare arrow keys walk the results without being stolen from
every other page. Unlike Help it does not swallow the rest — `Ctrl+1` still switches tabs from a
half-typed search.

`Tab` is deliberately **not** in the catalog. Accepting a ghosted completion belongs to the field
showing one (`GhostCompletionBox`), because whether there is a suggestion to accept is a property of the
focused control; routing it through the chain would mean asking a view model about focus, and would
stop `Tab` moving focus everywhere else.

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
`%AppData%` — which is how the persistence tests run against a temporary file. On Linux that resolves to
`$XDG_CONFIG_HOME` ?? `~/.config`, a different tree from the log's `$XDG_DATA_HOME` ?? `~/.local/share`,
so the two never collide.

## Paths across platforms

Two statics in `src/Shared` hold the assumptions that differ between Windows and Linux, so no caller has to
know which it is running on:

- **`PathComparison`** — how to tell whether two strings name the same path (`OrdinalIgnoreCase` on
  Windows, `Ordinal` elsewhere). For identity only: sorting and filtering names stay case-insensitive
  everywhere, because that is presentation rather than identity.
- **`SystemDrive`** — where the OS itself lives, in both shapes the app needs. `Letter` is the Windows
  drive letter that volume records are keyed by; **`Root` is the rooted path** (`C:\` or `/`), never empty,
  for anything that has to open or measure it.

Environment expansion is per-platform and belongs to `ToolkitPaths.Resolve` — `%VAR%` on Windows, `$VAR`,
`${VAR}` and a leading `~` elsewhere — never to a direct `Environment.ExpandEnvironmentVariables` call.
`Resolve` and `IsFileSystemPath` each keep an `internal` overload taking the platform as a flag, so a table
authored for one platform can be checked from the other.

A third static, **`TrayIntegration`**, holds a capability rather than a path convention: whether closing
the window may hide to a tray icon instead of exiting. Windows only — stock GNOME runs no
StatusNotifierItem host, and since the setting is on by default, honouring it there would hide the window
behind an icon that never appears. Nothing reliable can be asked at startup, so the app exits on close
wherever a tray is not guaranteed, and the setting is shown disabled rather than removed.

### Desktop integration

Four seams cover the parts of the app that talk to the *desktop* rather than the kernel, and each answers
the same question differently rather than degrading to nothing:

- **`IFileSystemRoots`** — what the File Explorer tree starts from. Drive letters on Windows; `/`, `$HOME`
  and removable mounts on Linux, the last derived from `/proc/mounts` by matching the `/media/`,
  `/run/media/` and `/mnt/` prefixes, which avoids resolving a user name.
- **`IStartupRegistration`** — the HKCU `Run` value on Windows, an XDG `.desktop` file under
  `~/.config/autostart` on Linux.
- **`IShellInterop`** / **`IProcessInterop`** — friendly type names and the Properties button. **No Linux
  desktop exposes a Properties dialog to a foreign process**, so rather than leaving the button dead both
  reveal the containing folder, where the desktop's own dialog is a right-click away.

Two of these carry a lesson about where per-platform code belongs. `LinuxShellInterop`'s type-name table
could not live on `FileTypeCatalog`, the obvious neighbour, because that class's static initialiser calls
`Geometry.Parse` and needs a render backend the test project deliberately lacks — so a map living there
would be untestable. And `LinuxProcessInterop` duplicates a few lines of `LinuxShellInterop` rather than
sharing them, matching what the Windows pair already do: the self-contained-tab rule outranks a six-line
saving.

### Copy is shared; content is not

Most seams answer "what is true of this machine". The Toolkit's answers something different — "what is
worth offering on this machine" — and it splits along a line worth naming, because the same line shows up
wherever a feature has both wording and data.

`IToolkitCatalog` carries only the **command set**, which is genuinely per-platform: `%appdata%` and
`taskschd.msc` mean nothing on Linux, and `journalctl` means nothing on Windows. The **copy** — the section
names, the badge labels, the display order — reads identically everywhere and stayed on the static
`ToolkitCatalog`, reached directly by the row model, the group model and the filter. Splitting it too would
have put a catalog reference on every row and bought nothing.

Two consequences worth knowing. The third arm, `UnsupportedToolkitCatalog`, is **empty rather than a
fallback to Windows'**: a platform with no table gets the page's own empty state and can still author its
own commands, which is more honest than thirty rows that can only fail. And the rules a table must satisfy
live in one place, `ToolkitCatalogInvariants`, asserted against **every** catalog rather than whichever the
host resolves — the tables are string literals, so a Windows run checks the Linux one too.

### Reading `/proc` and `/sys`

Linux exposes machine data as pseudo-files rather than an API, so the Linux providers are file parsers.
They all read through **`IProcFileSystem`** (`src/Services/Platform/Linux`) — `Exists`, `ReadAllText`,
`ReadAllLines`, `ListDirectory`, `ResolveLink` — rather than touching `System.IO` themselves.

That seam is infrastructure, not a provider seam, so it lives in its own `Services` folder like `IUiTimer`
rather than in a tab folder. Its purpose is testability: development happens on Windows, and without it no
Linux provider could be exercised until someone ran the VM. `FakeProcFileSystem` stages a tree of canned
fixtures, which is how the CPU and memory parsers are covered on both CI legs.

Implementations **never throw and hold no state** — a pseudo-file can vanish, change shape or deny access
mid-read, and all of that degrades to `null` or an empty list. Callers build paths by **string
concatenation with forward-slash literals**, never `Path.Combine`, which on Windows would produce
`/proc\stat`.

**Format knowledge lives in a parser beside the seam** — `ProcStatParser`, `ProcMeminfoParser`,
`ProcCpuinfoParser`, `OsReleaseParser`, `DmiIdReader`, `ProcMountsParser`, `ProcDiskstatsParser`,
`ProcNetParser`, and the four per-PID parsers behind the Processes tab — rather than in any one provider,
so a file read by two consumers is understood in exactly one place. One file gets one parser and the file
is in the name:
`ProcStatParser` reads `/proc/stat`, `ProcPidStatParser` reads `/proc/[pid]/stat`, and they are unrelated
formats. They parse
defensively, each against its own trap: by index with a length check, because the kernel has appended
columns to `/proc/stat` and `/proc/diskstats` over the years; by explicit unit, because `/proc/meminfo`
labels kibibytes as `kB` and leaves some lines unitless; by trimming around the colon, because
`/proc/cpuinfo` uses a varying number of tabs; by stripping only *matched* quotes, because `/etc/os-release`
is a shell fragment mixing quoted and bare values; by expanding octal escapes, because `/proc/mounts`
separates its fields with the space a mount point may itself contain; by splitting on the *last* `)`,
because a process's `comm` is parenthesised and may hold spaces and parentheses of its own; and by taking
only hierarchy `0` with an empty controller list, because a hybrid host's `/proc/[pid]/cgroup` surrounds the
unified v2 line with a dozen v1 ones; and by decoding each 32-bit word of an address separately, because
`/proc/net/tcp` prints them in *host* byte order rather than network order.

**The `/proc/net` byte order is the sharpest of those traps, because getting it wrong still produces
addresses.** `0100007F` is `127.0.0.1`, not `1.0.0.127`, and an IPv6 address is four words each reversed on
its own — reverse all sixteen bytes instead and a `::ffff:` mapping marker lands at the wrong end, giving a
wrong address that is still routable-looking. Nothing downstream can catch that, so it is pinned by
fixtures with known values, including a link-local address whose four words are all distinct.

Where two surfaces need the same *derived* numbers rather than the same file, the derivation is shared as
well: `CpuFacts` sits on `ProcCpuinfoParser` and feeds both the Dashboard's CPU tile and the Hardware tab's
Processor card, so the two cannot report different core counts for the same machine. `SysBlockFacts` does
the same for `/sys/block`, feeding the Storage tab's drive cards, its partitions table and the Hardware
tab's Storage Devices card. `ProcPids` is the smallest of these — just the all-digit entries under
`/proc` — but it is shared for the same reason: the Performance tab counts processes and the Processes tab
walks them, and only one of them should get to decide what a process is. `ProcPidName` was extracted for
that reason after the fact: the Processes tab names every process and the Network tab names each
connection's owner, and a second copy would have shown `systemd-resolved` on one tab and the 15-character
truncated `systemd-resolve` on the other. **These shared derivations report `""` and `0` honestly and let
each consumer apply its own placeholder**, because the placeholders genuinely differ — the Processes tab
wants "Unknown" where the Network tab wants "PID 1234", and a derivation that substituted early would have
destroyed the information needed to tell them apart.

**Reporting "not known" as `0` is only safe when `0` is impossible as a real reading.** That is the usual
case, and `CpuFacts` leans on it. It fails for the owner of a process: `/proc/[pid]/status`'s `Uid` is `0`
for root, so a denied read reported as `0` would move someone's own process into the System group. That
field is nullable, and the classifier is tested to prove an unknown owner is not treated as root.

**A record keyed by a platform's own identifier needs an equivalent derived, not invented.** The disk
records are all keyed by an `int` disk number, which on Windows is the OS's own — so the three providers
that fill them agree for free. Linux names disks `sda` and `nvme0n1`, and the answer is the kernel's
`major:minor` packed into that int, because it is readable from both `/sys/block/*/dev` and
`/proc/diskstats`: three separately-sampled readers still derive the same key from the same authority. A
positional index would have looked identical in a screenshot and drifted the moment a USB drive was plugged
in mid-run.

Filtering matters as much as parsing here, because the pseudo-filesystems describe far more devices than a
user has. `/sys/block` lists every snap loop device — around 25 on a stock Ubuntu GNOME install — and
`/proc/mounts` lists both those and some thirty pseudo-filesystems. The volume list applies a single rule
rather than an allowlist: keep a mount only when its device resolves to a disk that has a card. Mapper and
RAID devices are *resolved* through their `slaves` links rather than dropped, so an LVM or encrypted root
still lands on the drive that backs it.

**A permission-gated file is not exposed at all.** `/sys/class/dmi/id/{product_uuid,board_serial,
product_serial}` are root-only, so `DmiIdReader` offers named properties for the world-readable keys only,
rather than a general accessor that would let a caller reach a value which reads empty for every normal
user.

Where a platform genuinely has no source for a value, the provider returns `null` and the surface renders
"—". It does not substitute a near-miss. Five worked examples: the Performance tab's "Handles" tile is
blank on Linux because a Windows handle covers events, threads and registry keys as well as files;
`/proc/cpuinfo`'s `cpu MHz` never fills a *maximum* clock, because it is the instantaneous one and a
scaling governor would report an idle 800 MHz; the Motherboard card's PCIe slot count and the Processor
card's socket stay blank, because both live in SMBIOS tables the kernel does not surface without root;
drive health and temperature stay blank because both need SMART, which needs root; and the Processes tab's
per-process GPU column is permanently zero, because Linux exposes no rootless per-process GPU accounting at
all. All five are settled answers, not deferred work.

The Processes tab is where the discipline gets tested hardest, because the Windows classifier's inputs
simply do not exist: `EnumWindows` has no analogue on Wayland, where a client may not enumerate another
client's windows by design. Rather than collapse to a two-bucket approximation, the Linux classifier reads
`/proc/[pid]/cgroup`, which on any systemd distro already encodes the same distinction — world-readable, no
root, no display server, one small file per process. Its ordering carries the subtlety: a `.service` leaf
must be tested before the `app.slice` path, because modern systemd puts user *units* inside `app.slice`
alongside the scopes it launches apps into, so the other order would file every user daemon as a foreground
app.

The same discipline cuts the other way when a field *does* have a source. `/proc/diskstats` was easy to read
as two columns of transferred sectors, but the Storage tab's headline numbers and every sparkline on it
render *active time* — for which `io_ticks`, the milliseconds with at least one request outstanding, is the
exact analogue of the Windows counter. Reading only the obvious fields would have left the page rendering a
confident, permanent zero.

**A platform's value can need translating rather than reading.** Both platforms report a TCP connection's
state as a small integer, and the two numberings are unrelated: Linux `0x0A` is LISTEN where the Windows
`MIB_TCP_STATE` 10 is Last-ack, and Linux `0x01` is ESTABLISHED where MIB 1 is Closed. Since the display
table is keyed by the Windows numbering, the Linux interop translates into it rather than widening the
record — every row would otherwise carry a wrong but entirely plausible label, which no downstream check
could catch. The connections table is also where an unprivileged reader's limits show most plainly: a socket
belonging to another user cannot be attributed to a process at all, so its owner column shows "—" rather
than guessing.

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

Unit tests live in `tests/DashDetective.Tests` (xUnit, on the same neutral `net10.0` TFM as the app).
Fakes are small hand-written classes under `Fakes/` — there
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
