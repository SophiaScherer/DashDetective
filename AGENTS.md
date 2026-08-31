# AGENTS.md — DashDetective

> **This is a living document.** It will be updated as features are added, removed, or reworked.
> Always read this file in full before making any changes. If instructions here conflict with
> something you infer from the codebase, this file wins.

This file holds the **rules** — what a change has to satisfy. It is deliberately short, and the
detail it used to carry now lives in three files beside it.

## Before you change anything

Reading this file is **not sufficient**. It is step one of three, and steps two and three are not
optional background:

1. **Read this file in full.** It is the rules, and it is ~650 lines.
2. **Open [docs/SOURCE-MAP.md](docs/SOURCE-MAP.md) at the section for every folder you will edit.**
   It is one section per folder, so this is a targeted read, not a full one. Many entries record a
   bug that was already fixed once — the reason a value is read one way and not the obvious other
   way. **The code will not tell you.** `LinuxDiskTemperatureProvider` matching a hwmon by name
   rather than index, `/proc/diskstats` listing `sda` and `sda1` alike, `/proc/mounts` naming one
   device many times: each of those reads as an over-complication until you know what it prevents.
3. **Open [docs/FEATURES.md](docs/FEATURES.md) at the entry for the feature you are touching.** It
   holds the decisions inside it that must not be undone — the Toolkit's four safety invariants, for
   instance, are there and nowhere else.

Skipping 2 or 3 is how a fixed bug comes back.

| | |
| --- | --- |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | How the layers and seams fit together. Read first; it is short. |
| [docs/SOURCE-MAP.md](docs/SOURCE-MAP.md) | Every file under `src/`: what it is for, and the trap it avoids. |
| [docs/FEATURES.md](docs/FEATURES.md) | What each shipped feature does, and the decisions inside it. |
| [README.md](README.md) | Building, running and testing. |

## Project Overview

DashDetective is a system info console built with **Avalonia UI (C#)**. It was developed
incrementally, one feature at a time, in a modular style. Each feature lives in its own folder and
is largely isolated from the others.

The nine top-level features — Dashboard, File Explorer, Processes, Performance, Network, Storage,
Hardware, Toolkit and Settings — are **all built**. Keep changes inside the one a task names.

## Current Scope — READ THIS FIRST

**No feature is mid-build right now — every planned top-level feature is live.** Pick up only what a
new task explicitly assigns, and do not modify a live feature without an explicit scope expansion.

The nine tabs — Dashboard, File Explorer, Processes, Performance, Network, Storage, Hardware,
**Toolkit** and Settings — plus the shell **Navigation bar**, **universal search**, **keyboard
shortcuts**, the **page lifecycle** and the **widget system** are all live, as are two cross-cutting
passes (repo-hygiene / portfolio; de-duplication / composition). **Read
[docs/FEATURES.md](docs/FEATURES.md) before touching any of them** — it holds the write-up for each,
including the decisions inside it that must not be undone. The **widget system** entry changes how
every page is laid out, so read that one before touching a view.

A **customization pass** is also complete, and it reached five features rather than one folder:
**rebindable keyboard shortcuts** (the Settings Keyboard card, `ShortcutBindings` over the catalog's
defaults), **per-metric resource alerts** (`ResourceAlertWatcher`, now including GPU, disk activity and
low disk space), **multi-format diagnostics export** (text / JSON / Markdown / HTML / CSV over a
structured `DiagnosticsReport`), a **12/24-hour clock format**, and the **device account picture** in
the nav footer (`IUserPictureProvider`). Each has an entry in
[docs/FEATURES.md](docs/FEATURES.md) carrying the decisions inside it.

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
## Strict Working Boundaries

Every feature is live, so scope comes from **the task**, not from a list in this file. Edit the
folder(s) the task names and the shell code it needs — nothing else.

You may **read** anywhere in the repo for context (shared styles, naming, an existing seam), but
**editing** outside what the task asked for needs the user to say so first.

Do not:
- Create a folder for a feature that does not exist. There are no unbuilt features left.
- Refactor or "improve" an unrelated feature folder while working on the one you were given.
- Modify project-wide config, build files, or dependencies unless the task specifically requires it and the user has confirmed it.

If a task seems to require touching something outside what it named, stop and ask the user
before proceeding.

Before performing any of the following, stop and ask first:
- moving files
- renaming folders
- changing namespaces
- changing architecture
- introducing new dependencies
- changing MVVM approach
- altering project structure

## Folder Structure

Source lives under `DashDetective/src/`, split into three areas: shared building blocks,
the application shell, and one folder per feature ("tab"). All nine tabs exist — Dashboard,
File Explorer, Processes, Performance, Network, Storage, Hardware, Toolkit and Settings.

Source lives under `DashDetective/src/`, split into three areas: shared building blocks
(`Shared`), the application shell (`Shell`), and one folder per feature (`Tabs/<Feature>`), with
`Services` for anything more than one tab needs. Namespaces follow folders.

```
DashDetective/
  Program.cs, App.axaml, app.manifest, Assets/
  src/
    Shared/     ViewModelBase, page markers, formatters, Charts, Controls, Layout, Styles,
                Shortcuts, Completion
    Services/   SystemMetrics, Platform, Theming, Settings, Network, Search, Startup,
                Threading, Identity, Diagnostics
    Shell/      MainWindow, MainWindowViewModel, ViewLocator, Navigation, Search, Help,
                Shortcuts, TrayNotice
    Tabs/       Dashboard, FileExplorer, Processes, Performance, Network, Storage, Hardware,
                Toolkit, Settings
tests/DashDetective.Tests/   mirrors src/ path for path
```

**[docs/SOURCE-MAP.md](docs/SOURCE-MAP.md) is the file-by-file map** — what each file is for and,
where it matters, the trap it exists to avoid. Jump straight to the folder you are working in:

- Shared: [root](docs/SOURCE-MAP.md#srcshared) ·
  [Charts](docs/SOURCE-MAP.md#srcsharedcharts) ·
  [Controls](docs/SOURCE-MAP.md#srcsharedcontrols) ·
  [Layout](docs/SOURCE-MAP.md#srcsharedlayout) ·
  [Styles](docs/SOURCE-MAP.md#srcsharedstyles)
- Services: [SystemMetrics](docs/SOURCE-MAP.md#srcservicessystemmetrics) ·
  [Platform](docs/SOURCE-MAP.md#srcservicesplatform) ·
  [Theming](docs/SOURCE-MAP.md#srcservicestheming) ·
  [Settings](docs/SOURCE-MAP.md#srcservicessettings) ·
  [Network](docs/SOURCE-MAP.md#srcservicesnetwork) ·
  [Startup](docs/SOURCE-MAP.md#srcservicesstartup) ·
  [Diagnostics](docs/SOURCE-MAP.md#srcservicesdiagnostics)
- Shell: [root](docs/SOURCE-MAP.md#srcshell) ·
  [Navigation](docs/SOURCE-MAP.md#srcshellnavigation) ·
  [TrayNotice](docs/SOURCE-MAP.md#srcshelltraynotice)
- Tabs: [Dashboard](docs/SOURCE-MAP.md#srctabsdashboard) ·
  [FileExplorer](docs/SOURCE-MAP.md#srctabsfileexplorer) ·
  [Processes](docs/SOURCE-MAP.md#srctabsprocesses) ·
  [Performance](docs/SOURCE-MAP.md#srctabsperformance) ·
  [Network](docs/SOURCE-MAP.md#srctabsnetwork) ·
  [Storage](docs/SOURCE-MAP.md#srctabsstorage) ·
  [Hardware](docs/SOURCE-MAP.md#srctabshardware) ·
  [Toolkit](docs/SOURCE-MAP.md#srctabstoolkit) ·
  [Settings](docs/SOURCE-MAP.md#srctabssettings)

Feature-specific *providers* (static WMI/registry reads) live in the tab folder, not `src/Shared`,
until a second feature needs them.


Feature-specific *providers* (static WMI/registry reads) live in the tab folder, not `src/Shared`,
until a second feature needs them (per the "keep each tab self-contained" rule). Live **sampling**,
however, is now shared: `SystemMetricsService` owns one sampler per metric and drives it through a
`MetricChannel` at 1 Hz, fanning each sample out to the pages that subscribe (Dashboard, Performance,
Processes). A subscriber keeps its own 60-sample `MetricHistory` (two for network — download + upload)
and rebuilds its `Sparkline` via `history.Points(max)`, using `ChartScale.FitAxis` for the unbounded
network axis. Reuse these seams — do **not** re-inline a per-metric `DispatcherTimer`, declare a bare
`double[]` buffer, or write a bespoke points/peak helper.

## Platform seams

How a file that touches the OS is shaped, and what the analyzer will and will not catch.

### Provider seams

**Provider seams.** The OS-touching providers follow the `IGpuSensorReader` shape so they can be faked in
tests and given a second platform later. Most already do; the stragglers are listed under *What CA1416
does not catch* below. The idiom, established by `IStartupRegistration` (`src/Services/Startup`):

- An `internal interface I<Name>` in its own file, **in the folder the provider already lives in** —
  nothing moves, no namespace changes. Its doc comment states the never-throw / soft-fail contract.
- **The `Windows*` / `Unsupported*` split goes exactly where the platform-specific code is, and nowhere
  else.** A genuinely Windows-only reader becomes `internal sealed class Windows<Name>` carrying a
  **class-level** `[SupportedOSPlatform("windows")]` instead of an inner `OperatingSystem.IsWindows()`
  guard, plus an `Unsupported<Name>` **at the bottom of the same file**. `Unsupported*` is **the no-data
  contract for any platform without an implementation** — not "the non-Windows arm". It covers a platform
  whose milestone has not landed yet, and members a supported platform genuinely cannot supply (per-DIMM
  modules needs `dmidecode` with root; disk temperature on Linux). It follows that **if an implementation
  for a new platform would return exactly what `Unsupported*` returns, do not write the class** — leave
  that member on `Unsupported*` in that platform's arm, rather than accumulating empty `Linux*Provider`s. A provider that is **portable managed code keeps its plain name** and gains
  only an interface — naming it `Windows*` when it would run fine anywhere is a lie, and writing an
  `Unsupported*` twin for it would either duplicate the portable body or silently blank a panel that used
  to work. The Network tab is the worked example: `AdapterInfoProvider`, `ConnectionsProvider` and
  `DnsLookupProvider` stay unprefixed, while `IConnectionsInterop` and `IProcessNameResolver` each get the
  full set of arms. **`IProcessNameResolver` is the case worth studying** — its lookup is portable managed
  code (`Process.GetProcessById` runs anywhere), so by the API test it needed no seam at all. It gets one
  because of what it *encodes*: the `.exe` suffix and PIDs 0/4 are Windows facts, and off Windows they
  mislabel real rows rather than failing. **A class can be platform-specific by its knowledge rather than
  by its API surface, and CA1416 cannot see that kind at all.**
- One `ForCurrentPlatform()` picking between them — on the interface for a lone provider, on a bundle
  record (the `MetricSamplers` shape) for a set. That is the **only** place the platform is decided.
- **A view code-behind has no injection point** — `ViewLocator` builds views by name with a parameterless
  ctor. When code-behind needs an interop (it fetches the window handle for the native Properties dialog),
  it calls a small forwarder on the view model it has *already* resolved from `DataContext`, e.g.
  `vm.ShowProperties(handle, pid)`. Do not reach a static from a view.
- Consumers take the interface by constructor. A ViewModel with a parameterless ctor keeps it and chains:
  `public FooViewModel() : this(FooProviders.ForCurrentPlatform()) { }` + an `internal` injecting ctor, so
  `MainWindowViewModel` and `App.axaml.cs` are untouched.
- Everything new is `internal` — which is why `SettingsViewModel`'s ctor is now internal too (a public ctor
  cannot take an internal parameter type). Tests reach it all through `InternalsVisibleTo`.

**Never put `[SupportedOSPlatform]` on the interface** — every consumer would inherit the requirement and
light up CA1416 across the app. Adding a platform later = one new class per interface plus one line in
`ForCurrentPlatform()`; that is the whole point.

### CA1416, the gate that enforces it

**CA1416 is what enforces all of the above.** Both projects build on the neutral `net10.0` TFM with
`TreatWarningsAsErrors`, and neither carries a `NoWarn` for it, so an unguarded call to a Windows-only API
fails the build on both CI legs. Put the attribute on the **narrowest thing that is genuinely
platform-specific**: the type when the whole type is (`WindowsGpuAdapterProvider`), or a single ctor or
method when only that is. The Hardware tab is the worked example — `IHardwareInfoProvider`'s `Windows()`
factory resolves the WMI readers and carries the attribute, while `HardwareInfoProvider`'s composition and
per-card guard are portable, keep no platform in their name, and stay callable from tests on every
platform. (Until M7 the attribute sat on that composer's public ctor, for the same reason; splitting the
factory out is what let a second platform reuse the composition.) Where a guard cannot be seen across a
method boundary, the
attribute restates it: `WindowsSearchIndex.ReadHit` is annotated because its only caller `Run` holds the
`OperatingSystem.IsWindows()` check.

**What CA1416 does not catch.** It fires only on APIs that are themselves annotated — BCL surface like
`System.Management`, OleDb and `Microsoft.Win32.Registry`. **A hand-written `DllImport` declaration carries
no annotation, so a P/Invoke file is invisible to the analyzer.** Annotating those classes is therefore not
busywork: the attribute is the only thing that makes them visible *at their call sites*. Never conclude
from a clean build that the platform surface is covered — grep for `DllImport` and `LibraryImport`.

**Every class that reaches a native Windows API is now covered.** The table that used to sit here is empty;
M13 closed the last three rows, though not in the way it expected — see below.

**A `DllImport` class is covered by the constructor of the type that owns it, not necessarily its own.**
`AdlInterop`, `NvmlInterop` and `NvApiInterop` are `static` classes, so there is no constructor to annotate
and the only options are the type or each member. M13 measured what type-annotating all three costs:
**38 CA1416 errors**, every one inside `AmdGpuSensorReader` and `NvidiaGpuSensorReader`. Two of them are
*const* references (`AdlInterop.MaxSensors`, `NvApiInterop.MaxThermalSensors`) sitting in **field
initialisers** — the one place the rule above says an attribute cannot go. Clearing them would mean
annotating the two reader *types*, which drags their pure decode statics (`SelectTemperature`,
`IsDiscrete`, `SelectGpuSensorIndex`, `PlausibleCelsius`, `PlausibleWatts` — around 30 assertions) behind an
`IsWindows()` guard and costs the Linux leg all of it, for no added protection.

So the attribute goes on the **two reader constructors** instead, which is the real boundary: the interops
are `internal` and reachable only through a reader instance, and a reader can only be constructed inside
`WindowsGpuSensorProvider.CreateReaders()`, which is itself only reachable behind the guard in
`IGpuSensorProvider.ForCurrentPlatform()`. Verified by deleting `CreateReaders()`'s attribute and watching
both reader constructors light up CA1416 individually. The lesson generalises: **when the P/Invoke lives in
a static class, annotate whatever must be constructed to reach it.**

**M13 cleared `GpuUsageSampler`**, the same way M8 cleared its own: two view models held
`private readonly GpuUsageSampler _gpuSampler = new()` and `DeviceInventory` constructed a third inline, so
`IGpuUsageSampler.ForCurrentPlatform()` had to own the construction before the class could be renamed
`WindowsGpuUsageSampler` and annotated. The combined `Sample()` / `SampleEngines()` pair was left off that
seam — no consumer had called either since the multi-GPU split — and then deleted outright once the seam
made it plain they were unreachable.

**M8 cleared the fifth**, and it is the worked example of why the seam has to come first: three view models
held `private readonly PhysicalDiskThroughputSampler _throughputSampler = new()`, and there is nowhere on a
field initialiser to put the attribute. Introducing `IPhysicalDiskThroughputSampler` moved the construction
into one guarded `ForCurrentPlatform()`, and only then could the class be renamed `Windows*` and annotated.
Removing that guard now fails the build — which is the check worth running when adding an attribute, since a
decorative one and a load-bearing one look identical in a passing build.

**The converse case, from M12: a `Windows*` class that must *not* be annotated.** `WindowsToolkitCatalog`
sits behind `IToolkitCatalog.ForCurrentPlatform()` exactly like the samplers do, but it reaches no platform
API at all — it is a table of string literals — so `[SupportedOSPlatform("windows")]` would fail the
delete-the-guard test outright. It also could not be honoured: `Instance` is a `static` field, initialised on
class load *outside* any `IsWindows()` guard, which is the field-initialiser trap M8 hit from the other
direction. **A `Windows*` name is not on its own a reason to annotate** — ask what annotated API the class
touches, and if the answer is none, leave it off and say why in the class doc.

**M5 annotated its four on the *constructor*, not the type, and later milestones should copy that.** A PDH
sampler's `Sample()` and `Dispose()` are guarded by its own `Ready`/`_ready` flag and are genuinely callable
anywhere, as are pure statics like `WindowsLogicalProcessorSampler.TryParseInstance`. Annotating the type
would have dragged all of that behind an `IsWindows()` guard and cost the Linux leg its coverage of the
inert-contract tests and the instance-name parser, for no gain — the attribute's whole job is to make the
*constructor* visible at its call site, which is what forces `CpuUsageSampler`'s ctor and each
`ForCurrentPlatform()` to hold a guard. This is the Hardware tab's shape, applied to samplers.
M6 followed it for both of its classes, including the two whose Windows arm has an **empty** constructor —
an empty annotated ctor is not a wasted one, it is the whole enforcement point, and it kept
`WindowsSystemPerformanceProvider.ToBytes` and the samplers' `SamplerInit.Inert` seam covered on the Linux
leg. M6 also shows what the seam buys beyond the attribute: with `ForCurrentPlatform()` deciding, the inner
`OperatingSystem.IsWindows()` guard inside `Read()` became dead weight and was removed.

### Path hygiene

**Path hygiene — the other thing CA1416 cannot see.** A path assumption is runtime behaviour, not annotated
API surface, so the analyzer is silent on every one of them: on Unix `\` is an ordinary filename character,
`Path.GetPathRoot(@"C:\x")` is `""`, `Path.IsPathRooted(@"C:\x")` is `false`, `%VAR%` never expands, and
`FileAttributes.Hidden` is a leading dot rather than a bit. M4 fixed the existing ones and left three rules
that keep them from coming back:

- **`Shared/PathComparison` decides whether two strings name the same path** — `OrdinalIgnoreCase` on
  Windows, `Ordinal` elsewhere. Use it for a path-keyed `HashSet`/`Dictionary`, a "did we navigate" check
  or a dedupe. **Do not use it for sorting or filtering names**, which stay `OrdinalIgnoreCase` on every
  platform: those are presentation, and someone typing "doc" expects to find "Documents" whatever the
  filesystem thinks. Getting this backwards silently merges two real folders on Linux.
- **`ToolkitPaths.Resolve` owns environment expansion**, because the notation is per-platform (`%VAR%` on
  Windows; `$VAR`, `${VAR}` and a leading `~` elsewhere). Never call
  `Environment.ExpandEnvironmentVariables` directly — off Windows it is an identity function. Its
  `internal Expand(target, windows)` seam is how both arms stay testable from either dev machine, and
  `IsFileSystemPath(target, windows)` carries the same shape one level up, so the Linux catalog's folder
  rows can be proven reachable by the in-app File Explorer from Windows. **That seam only carries one
  way.** `windows: false` answers correctly on any host, because the Unix notation is expanded here in
  managed code; `windows: true` still needs the variable to *exist*, and `%appdata%` does not on a Linux
  runner. A test asserting the Windows notation keeps its `IsWindows()` guard — passing the flag does not
  remove the need for it, and M12 shipped that mistake briefly before the `OperatingSystem.IsLinux` grep
  caught it.
- **Tests whose subject calls `Path.*` build paths through `Fakes/TestPaths`** (`Root`, `Of(...)`,
  `Dir(...)`), never from drive-letter literals. Where a path is only an opaque token being round-tripped
  — `NavigationHistory`, `RecentSearches` — the literals are clearer and stay. **A forward-slash literal
  is not a safe shortcut either**: `Path.GetDirectoryName` normalises its *result* to the running host's
  separator, so asserting `/home/sophia` fails with `\home\sophia` on a Windows box even when the reader
  is correct. M11 hit exactly that. `Path.GetFileName` does not rewrite anything, so `/`-shaped inputs
  are fine there — the distinction is whether the method returns a path or a segment.

Two traps worth knowing, both found the hard way: a literal reaches `Path.*` **transitively** far more often
than a grep suggests (`ToolkitCommandFactory.ToEntry` → `CanOpenInApp` → `IsPathRooted` was the one that got
through), and `Process.Start` with `UseShellExecute` **succeeds** on Linux where it throws on Windows,
because the target is handed to `xdg-open`. Assert on structure, not on the OS's error text.

`Shared/SystemDrive` carries both shapes of "where the OS lives": `Letter` for matching a Windows volume
record, `Root` for anything that has to open or measure it (`C:\` or `/`, never empty).

### `/proc` access

**`/proc` access — the Linux counterpart, added in M5.** Every read of `/proc` or `/sys` goes through
`Services/Platform/Linux/IProcFileSystem`, never `System.IO` directly. It exists so Linux providers are
unit-testable from a Windows dev box against canned fixtures (`Fakes/FakeProcFileSystem` +
`Fakes/ProcFixtures`); a provider that opens files itself cannot be tested until someone runs the VM. Three
rules:

- **Build these paths with string concatenation and forward-slash literals — never `Path.Combine`.** On
  Windows `Path.Combine("/proc", "stat")` yields `/proc\stat`, and every fixture lookup then silently
  misses. This is the `/proc` analogue of the drive-letter trap above, and just as invisible to CA1416.
- **`IProcFileSystem` implementations must be stateless and must never throw** — a pseudo-file can vanish,
  change shape or deny access under the reader, and all of that degrades to `null`/empty. Statelessness is
  what lets a provider built on it go into `HardwareProviders`; note that a *sampler* holding a previous
  snapshot (`LinuxCpuSampler`, `LinuxLogicalProcessorSampler`) is stateful and must not.
- **Format knowledge lives in a parser beside the seam, not in a sampler** — `ProcStatParser` is shared by
  the aggregate and per-core CPU samplers, `ProcMeminfoParser` by the memory sampler and the system-counters
  provider, `ProcCpuinfoParser` by `CpuFacts` and the frequency sampler, `OsReleaseParser` and `DmiIdReader`
  by the Dashboard's identity panel and the Hardware tab's Motherboard card, `ProcMountsParser` and
  `ProcDiskstatsParser` by the volume provider and the throughput sampler, and the four per-PID parsers
  (`ProcPidStatParser`, `ProcPidStatusParser`, `ProcPidIoParser`, `ProcCgroupParser`) by the Processes tab's
  walk and its classifier. **One file, one parser, and the file is in the name** — `ProcStatParser` reads
  `/proc/stat` while `ProcPidStatParser` reads `/proc/[pid]/stat`, which are unrelated formats. Parse **by
  index with a length
  check**: the kernel has appended columns to `/proc/stat` over time, so 7-column and 10-column forms both
  have to work, and `/proc/diskstats` grew from 14 fields to 18 in 4.18 and 20 in 5.5. The same defensiveness
  applies to units — `/proc/meminfo`'s `kB` is kibibytes, some of its lines carry no unit at all, sysfs cache
  sizes are suffixed (`8192K`, `16M`) rather than bytes, and both `/sys/block/*/size` and `/proc/diskstats`
  count **512-byte sectors** regardless of the drive's physical sector size.
  Where two cards want the same *derived* numbers rather than the same file, the derivation is shared too:
  `CpuFacts` exists so the Dashboard tile and the Processor card cannot report different core counts,
  `SysBlockFacts` so the Storage tab and the Hardware tab cannot disagree about a drive, and `ProcPids` so
  the Performance tab's process count and the Processes tab's walk cannot disagree about what a process is.

**Not every "not known" can be reported as 0.** The `CpuFacts` convention — report `""`/`0` honestly and let
each consumer place its own placeholder — is right whenever 0 is impossible as a real reading. It is *wrong*
for `/proc/[pid]/status`'s `Uid`, where 0 means root: a denied read reported as 0 silently moves a user's
process into the System group. That field is `int?`, and `LinuxProcessClassifier` has a test pinning that an
unknown owner is not treated as root. Check what 0 means in the domain before reaching for the convention.

**Where a record is keyed by a platform's own identifier, derive an equivalent — do not invent one.** M8's
case: `PhysicalDiskInfo`, `VolumeInfo` and `DiskThroughputSample` are all keyed by an `int` disk number,
which on Windows is the OS's own, so three separately-sampled providers agree for free. Linux names disks
`sda`/`nvme0n1`, and the answer is the kernel's `major:minor` packed as `(major << 20) | minor`
(`SysBlockFacts.Pack`), because it is readable from both `/sys/block/*/dev` and `/proc/diskstats` — so all
three still derive the same key from the same authority. **A positional index would have been the trap**: it
drifts the moment a USB drive is plugged in mid-run, and only between two of the three readers.

**`/proc/mounts` needs two defences a first pass misses.** It lists the same device many times (bind mounts,
`/var/snap`, btrfs subvolumes) and `StorageComposer` *sums* a disk's volumes, so without a dedupe a drive's
capacity and used space multiply. And its device/mount-point fields are octal-escaped, because the separator
is a space.

Three `/proc` gotchas worth knowing: `/proc/stat` lists **online** CPUs only, so per-core state must be keyed
by core number and a core appearing mid-run reports 0 until it has an interval (diffing it against zero
reports its whole since-boot average — this shipped as a bug in M5 and a test caught it); `/proc/cpuinfo`
separates key from value with **tabs**, so parse by trimming around the colon, never by layout; and
`/etc/os-release` is a **shell fragment**, so the same file mixes quoted and bare values.

**Permission-gated sysfs files: do not expose them at all.** `/sys/class/dmi/id/{product_uuid,board_serial,
product_serial}` are mode **0400** — root-only. `DmiIdReader` therefore offers named properties for the
world-readable keys *only*, rather than a general `Value(key)` that would let a caller reach a field which
silently reads `""` for every normal user and logs a denial on every real machine. M7's tests pin this by
asserting `FakeProcFileSystem.Reads` never contains a `*serial` or `*uuid` path — the one degradation that
is invisible in the rendered output.

The **System Information** panel reuses the same async provider pattern on both platforms
(`GetAsync() => Task.Run(Read)`, per-section soft-fail → "Unknown …") reading the static identity facts
once at startup into a `SystemStaticInfo` record; `HardwareProviders.ForCurrentPlatform()` picks the arm.
`WindowsSystemInfoProvider` also reads the **registry** (via the in-box `Microsoft.Win32.Registry` API) for
the build revision (`UBR`) and feature-update label (`DisplayVersion`), which WMI does not expose;
`LinuxSystemInfoProvider` puts the **kernel release** in that same Build row, the closest analogue Linux
has. **Uptime** is the one
live value with no sampler/provider — the VM formats `Environment.TickCount64` (the 64-bit,
non-wrapping tick count) on its own coarse 30 s `DispatcherTimer` (uptime's smallest displayed unit is
minutes). Verbose vendor strings (e.g. "American Megatrends International, LLC.") are shown **in full**;
`InfoRow` wraps them flush-right rather than trimming.

### Network sampling

**Network sampler gotcha (important).** `NetworkUsageSampler` samples a **single primary adapter**,
never a sum of all adapters. On .NET, `NetworkInterface.GetAllNetworkInterfaces()` returns many
virtual/filter/phantom adapters (Hyper-V, VirtualBox, WFP, …) that **mirror the physical NIC's byte
counters**, so summing them multi-counts the same traffic (was ~8× too high vs Task Manager). Note a
Windows PowerShell 5.1 probe will **not** reproduce this — .NET Framework returns far fewer adapters
than modern .NET. The sampler selects the internet-facing adapter (Up, non-loopback/tunnel, has a
usable default gateway, busiest by bytes), locks to its `Id` across ticks, and matches Task Manager's
per-adapter numbers. When verifying throughput, always cross-check the actual value against Task
Manager, not just "looks plausible".

### Theming

**Theming (runtime light/dark + accent).** Colours live in `Palette.axaml` in three groups:
*theme-variant* keys (surfaces, lines, text ramp, hover overlays) sit in
`ResourceDictionary.ThemeDictionaries` under `Dark`/`Light` and flip with the app's `ThemeVariant`;
the *accent set* (`Accent`, `AccentHover`, `OnAccent`, `AccentSoft`, `AccentColor`/`AccentDeep`) and the
per-graph *chart-series* keys (`ChartCpu`, `ChartMemory`, `ChartGpu`, `ChartStorage`, `ChartNetDown`,
`ChartNetUp`) sit top-level and are **swapped at runtime**. **Rule:** any key that can change at runtime
must be referenced with `{DynamicResource ...}`, never `{StaticResource}` (only the fixed legend colours
`Blue`/`Green`/`Purple`/`Orange`/`Yellow` stay static). `ThemeService` (`src/Services/Theming`) is the
**only** code that writes to `Application.Current` — `ApplyTheme` sets the variant; `ApplyAccent` swaps
the accent and installs the palette derived from it; `ApplyDefaultAppearance` restores the authored one.
It's constructed once in `MainWindowViewModel`, applied at startup, and handed to `SettingsViewModel` and
`PerformanceViewModel`. Note this feature deliberately touched shared styles + the shell
(Palette/SharedStyles, MainWindow, NavItem) — theming is cross-cutting, so it lives in `src/Services`,
not a tab.

**An accent re-hues the graphs; it must never flatten them.** Selecting an accent used to set all six
chart-series keys to that one colour, which erased the per-metric coding the charts depend on — worst on
the Dashboard's Network Throughput chart, where download and upload share an axis and became one
indistinguishable line. `ChartPalette.Derive` now rotates the whole authored palette instead: the accent
becomes the CPU (and net-down) series, and every other series keeps its own saturation and lightness while
its hue turns by the accent's offset from the default blue. The authored spacing between hues therefore
survives whatever accent is chosen, and `Derive(AccentPreset.Default.Color)` reproduces `ChartPalette
.Default` exactly, so the blue swatch and the "Default" swatch agree. **`ChartPalette` is the single
source of these colours** — do not parse a series hex anywhere else. The Performance tab used to, giving
the app two contradictory answers to "what colour is CPU"; its `ResourceRow`s now carry a `ChartSeries`
identity and resolve the brush through `ThemeService.BrushFor`, re-applying on `SeriesChanged`. Status
colours are a different thing and stay fixed: Storage's health pills, its usage bars and File Explorer's
type glyphs must not follow an accent — "Healthy" is green whatever the user picked.

### Charting

**Charting conventions.** Every live chart is the shared `Sparkline` on a fixed 0–100 axis, drawn from a
`MetricHistory` via `history.Points(max)`, with a gradient `Fill` and a `ShowGrid` lattice. The grid is a
**scale, not decoration** — its four bands are quarters of the axis, and the middle line is the value the
panel charts label "50%" — so do not give one chart its own row count; a card ruled into halves marks
different values than the chart beside it. Axis furniture is graded by how much room a chart has:

- **The Performance detail chart** (`chartHero`) carries the lot, and is the only one that does: a caption
  of what it plots and over how long, a label on **every** grid line both ways (`AxisValueLabels` /
  `AxisTimeLabels`), both axis titles (`AxisYTitle` / `AxisXTitle`) and a `StatusText`. It has a pane to
  itself, which is what pays for the furniture.
- **A chart in a panel of its own** — `chartPanel` and the Network tab's `chartMini` throughput traces —
  carries a caption, **three** value labels, the two ends of the time range, and a `StatusText`. It is the
  enclosing panel that decides this, not the size class.
- **Stat cards** (`chartMini` inside `StatCard`) carry the axis **ends only** and no time range — three
  labels would touch at a card's height and a footer would take most of the plot, and the cards share the
  window the panel charts below them state. Their ceiling is per-card (`DashboardCard.AxisMaxLabel`): most
  plot a percentage, but the network card fills to its own live peak, so a blanket "100%" lies on it.
- **Per-core / per-engine cells** (`chartCell`) carry the axis **ends only** and no time range, on the stat
  cards' reasoning: a cell is a third the height of the chart above it. They tiled bare until the grid
  widened to fit a gutter (`MinItemWidth` 140), since a cell whose neighbour is at 90 % and whose own trace
  is flat says nothing without one.

Grade by the room a chart has, and reserve nothing it does not use: every label property is opt-in and
`ChartAxis` returns a zero gutter or footer for one that is empty, which is what keeps an unlabelled chart
measuring exactly as it did before the furniture existed.

Never hardcode a window in a caption or on a time axis: it is the refresh interval times the slot count, so
say it through `ChartWindow.Describe` / `StartLabel` / `TickLabels`. The oldest end reads as elapsed time
("60s ago"), never as a negative offset — a leading minus suggests a value below zero on a chart whose y
axis starts there — and only the oldest label says "ago", the rest of the row inheriting its sense.

A label set must land on the grid lines it is read against: `PercentLabels` / `RateLabels` / `TickLabels`
take the band count, and `ChartAxis.FitLabelCount` drops a set too big for its plot to a sparser one that
still lands on lines, never below the two ends.

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
  `SystemMetricsService` (live sampling), `MetricHistory` + `ChartScale` + `ChartAxis` +
  `ChartWindow` + `ChartStatus` (charts), `ChartPalette` (series colours),
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
`Microsoft.Win32.Registry` (the HKCU `Run` key) are in-box on `net10.0`, and Avalonia's
`TrayIcon` ships with the framework. Reuse the in-box JSON + registry for future persisted state.

**The full referenced set, as the csproj actually has it** — the paragraphs above each describe one
piece of work, so read this for what is present today: Avalonia (`Avalonia`, `.Desktop`,
`.Themes.Fluent`, `.Fonts.Inter`), `CommunityToolkit.Mvvm`, `System.Management` (WMI),
**`System.Data.OleDb`** (universal search's file results, through the Windows Search index's
`Search.CollatorDSO` OLE DB provider) and **`AvaloniaUI.DiagnosticsSupport`** (Debug-only; excluded
from every other configuration in the csproj). The last two are the ones the paragraphs above do not
mention. Everything else is in-box.

## Testing conventions

Unit tests live in **`tests/DashDetective.Tests`** (xUnit, `net10.0`, referenced by `DashDetective.sln`).
CI builds, format-checks, tests **and** collects coverage on `windows-latest` and `ubuntu-latest`, in
Debug and Release — four legs, none of them guarded. `dotnet format` gates the test code on both, so keep
usings alphabetical (`System` is **not** sorted first).

**A green Windows run proves nothing about the Linux leg.** Reader-identity tests branch on the host, so
the arm asserting Linux never executes locally. Before calling any work that touches a
`ForCurrentPlatform()` green, grep the test project for **both**:

- `OperatingSystem.IsLinux` — the tests that already know about three platforms.
- `IsType<Unsupported` — **the one that actually catches it.** A two-arm test reads
  `if (IsWindows()) … else Assert.IsType<Unsupported…>` and contains no `IsLinux` at all, so the first
  grep walks straight past it. M11 added a Linux arm to `IShellInterop` and only this second grep found
  `ShellInteropTests`, which would have failed the Ubuntu leg.

To prove a fix without CI, invoke the private factory by reflection from a throwaway test
(`typeof(HardwareProviders).GetMethod("Linux", BindingFlags.Static | BindingFlags.NonPublic)`), assert the
types on Windows, then delete it.

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
  - `IProcFileSystem` + `ProcFileSystem` (`src/Services/Platform/Linux`) — the same shape for `/proc` and
    `/sys`, so every Linux provider is testable from a Windows box against `FakeProcFileSystem` fixtures.
    Each Linux sampler and provider takes it by ctor, with a parameterless chain for production.
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

## Code conventions

These are the rules that decide whether a change is right, as opposed to how the app is put together —
that is [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

### Comments

Terse. One or two sentences, no paragraphs, no multi-line essays. Keep XML docs on public members but
keep them short. Say *why* where it is not obvious; never restate what the code already says.

### Soft-fail: what a fallback owes you

"Degrades to a neutral fallback" is the rule. Four ways it gets broken:

- **A fallback must not be a confident wrong answer.** `0` is neutral only where `0` is impossible as a
  real reading. It is not neutral for a utilisation percentage — `MetricChannel` stops polling after a
  failure, so a `0%` sits there claiming an idle machine for the session. Use `Placeholders.NoReading`.
- **A fallback must not be unreachable.** A sampler that catches its own failure and returns zero never
  lets `MetricChannel` call `_onFailed`, so the placeholder can never appear. Samplers **throw**; the
  channel turns that into the placeholder. Returning a zero *measurement* is different and fine.
- **A fallback must leave the surface in one piece.** Build into a local and swap at the end, so
  everything that can throw happens before anything on screen is touched.
- **A `catch` must not be wider than its comment.** Filter it:
  `catch (Exception e) when (e is ArgumentException or Win32Exception)`. Keep the `try` no wider than
  the failable call. Every bare `catch { }` carries a comment saying why nothing is done.

**Who logs:** providers, samplers and services, through `Services/Diagnostics/Log`. View models and
code-behind do not — the provider that failed has already logged it with more context.

**A sanity window is a shared constant, not a per-reader one** (`GpuSensorRange`,
`DiskTemperatureRange`). They stay separate from each other on purpose: a drive at 130 °C is a bad
reading, a GPU at 130 °C is a hot one.

### Settings

- **Adding a property to `AppSettings` is not enough to give it a default.** `SettingsStore.Load`
  deserializes a file *over* `AppSettings.Defaults`, key by key, and that merge is what preserves
  initializers — **load-bearing, not belt-and-braces.** The JSON source generator treats a record's
  `init` properties as constructor parameters (generated code cannot assign an `init` property after
  construction, so it must use one object initializer), builds the object from a single args array, and
  fills absent slots with `default(T)`. Deserializing directly therefore discarded every non-default
  initializer for any property a file omitted: `ShowInTray` loaded as `false` for months, and every
  alert threshold would have loaded as `0`. Do not "simplify" `Load` back to a plain `Deserialize`.
- **Collection-shaped state is one opaque encoded string with its own codec**, because the record's
  value equality — which the save round-trip relies on — compares collections by reference. Codecs store
  **names, never ordinals**: inserting an enum member must not silently re-point saved state at a
  different one. An unreadable entry is skipped, not fatal.
- **A setting that can be switched off encodes "off" in the value where that is unambiguous.** An alert
  threshold of `0` means "not watched", which keeps one control per row and one check in the watcher.
  Where the *number* has to survive being switched off, the switch is a separate persisted flag — that
  is why the alert rows store both, so GPU can ship off with 90 already in the box.
- **`SettingId` and `SettingCatalog` are a bijection**, pinned by a test: a new id without an entry
  fails the build. The page's labels bind to the catalog rather than holding literals, so the copy on
  screen is by construction the copy universal search matches against.

### Platform readers

- **Never substitute a near-miss.** Where a platform has no source for a value, return `null` and let
  the surface render "—". Settled examples: the Performance tab's Handles tile on Linux, a maximum CPU
  clock from `/proc/cpuinfo`, SMBIOS-only board fields, SMART-only drive health, per-process GPU on
  Linux.
- **Reporting "not known" as `0` is only safe when `0` is impossible.** It fails for a process owner:
  `Uid` is `0` for root, so a denied read reported as `0` moves someone's process into System.
- **A record keyed by a platform's identifier needs an equivalent derived, not invented.** Disks are
  keyed by the kernel's `major:minor`, GPUs by PCI address, both taken from one shared derivation — if
  two readers derived a key separately and disagreed, `DeviceInventory`'s intersection would empty and
  every card would vanish with nothing logged.
- **One file, one parser, named after the file.** Parse defensively: by index with a length check, by
  explicit unit, and mind the traps — `/proc/net`'s host byte order (`0100007F` is `127.0.0.1`), the
  parenthesised `comm` in `/proc/[pid]/stat`, octal escapes in `/proc/mounts`.
- **A permission-gated file is not exposed at all** — `DmiIdReader` offers only world-readable keys.
- Build `/proc` paths by string concatenation with forward slashes, never `Path.Combine`.

**Unverifiable on CI:** neither the VM nor `ubuntu-latest` has a discrete GPU or a SMART-capable disk,
so no Linux temperature, power or utilisation *value* has been checked against real hardware. What is
verified is scale (millidegrees, microwatts, bytes, bare percent), that an absent source degrades to
"—" rather than zero, and that enumeration and sampler agree on the adapter key. Finding no SATA drive
temperature is the expected outcome, not a defect.

### Styles

- **A style used by a second tab must be promoted** to `SharedStyles.axaml`. Four tabs had independently
  defined `colHead` and drifted to three variants of it.
- **A shared style can only be overridden by a local one, never the reverse.** Avalonia ranks styles by
  how close their host is to the control, so a `<UserControl.Styles>` rule beats an app-level rule
  regardless of selector specificity. `Border.settingRow` set `Background="Transparent"` locally, which
  silently outranked `Border.revealFlash.highlighted` and left Settings with no reveal flash — build,
  format and 2246 tests all green. After promoting a style, delete the local same-property setter.
- **Layer rather than restate.** `Classes="bare rowRun"`, `Classes="card selectable"` — not a new style
  repeating six setters.
- **Dimensions are adopted by contact, not by sweep.** New code uses the `Dimensions.axaml` tokens; a
  view converts only lines it was already editing. A literal that has a token is a defect; one that
  does not is fine until a second site needs it. The corollary bites hardest: **a token with no call
  site should not exist.** `Dimensions.axaml` shipped with eighteen keys and nine users; the nine
  spare ones were guesses at what would be wanted, which is the same aspirational cruft the rule
  exists to stop. Add a token when the second site asks for it, not before.
- A control or style used by one tab stays tab-local. A panel repeated within a single feature stays in
  that feature (the Network tab's `ConsolePanel`).
- **`Palette.axaml` owns every colour in the app**, pinned by `PaletteOwnershipTests`. The exemptions
  are the three C# mirrors beside it and **`ReportFormatters.cs`** — an exported HTML report is a
  browser document with no access to the theme, and one that only looked right inside DashDetective
  would be the bug. That is the bar for a future exemption: rendered outside the app, not merely
  inconvenient to tokenise.

### Testing

- **A fake must be able to break its contract on demand.** A fake that only ever succeeds makes its
  subject's `catch` unreachable, so the soft-fail rule goes unverified. Every shared fake has an opt-in
  failure mode (`FakeProcFileSystem.ThrowOn`, `FakeGpuUsageSampler.Throwing`, …).
- **Test the denial against a fixture that would otherwise succeed.** A provider handed an *empty* fake
  returns the same `Unknown` it returns when denied, through a different path — so the test passes
  whether or not the `catch` exists. Stage a working fixture first, then deny it.
- **Pure logic belongs outside a platform-gated class**, or it never runs on the Linux CI leg.
- **Never assert a measured width or height.** Font metrics differ between the two CI legs; pass
  dimensions into the arithmetic instead, as `ChartAxis` and `WidgetBoardLayout` do.

### Quality gates

- `.editorconfig`: four-space indent, file-scoped namespaces, K&R braces, broad `var`. Usings
  alphabetical with `System` **not** first.
- The build sets `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild` and `AnalysisLevel=latest`, so a
  style or platform-compatibility issue fails the build.
- CI runs `dotnet format --verify-no-changes` before building, then the suite with coverage, on
  `windows-latest` and `ubuntu-latest` in Debug and Release.
- Build and test with `--artifacts-path`: a running app or an IDE holding `bin/` causes MSB3027.

## Working Style

- One detail at a time. Prefer small, focused changes over broad sweeps.
- Match the conventions already in the codebase (naming, MVVM patterns, styling) rather than
  introducing new ones. Read a neighbouring tab before inventing anything.
- If you're unsure whether something is in scope, ask rather than assume.

## Updating This Document

This file holds **rules** — what a change must satisfy. Detail goes elsewhere, and putting it here
is what made this document 2,500 lines once already:

| New detail | Belongs in |
| --- | --- |
| What a file is for, or a trap inside it | [docs/SOURCE-MAP.md](docs/SOURCE-MAP.md) |
| What a shipped feature does and why | [docs/FEATURES.md](docs/FEATURES.md) |
| How the layers and seams fit together | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| How to build, run and test it | [README.md](README.md) |

Update **Current Scope** when work starts or finishes, and **delete** what stops being true rather
than adding a correction beside it.
