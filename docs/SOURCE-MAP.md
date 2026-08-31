# DashDetective — Source Map

A file-by-file map of `DashDetective/src/`: what each file is for and, where it matters, the trap it
exists to avoid. Most of the value here is the second kind — a note saying *why* something is done the
way it is, so a later change does not undo a fix.

Read this when you need to find or change a specific file. For the shape of the system — the layers,
the seams and how they fit — read [ARCHITECTURE.md](ARCHITECTURE.md) first; it is much shorter. The
rules that decide whether a change is *right* are in [AGENTS.md](../AGENTS.md).

Namespaces follow folders. Anything a second tab needs moves to `Shared`/`Services`; everything else
stays in its tab folder.

## Contents

- [Bootstrap](#bootstrap)
- [`src/Shared`](#srcshared)
- [`src/Shared/Charts`](#srcsharedcharts)
- [`src/Shared/Styles`](#srcsharedstyles)
- [`src/Shared/Layout`](#srcsharedlayout)
- [`src/Shared/Controls`](#srcsharedcontrols)
- [`src/Services/Settings`](#srcservicessettings)
- [`src/Services/Startup`](#srcservicesstartup)
- [`src/Services/Identity`](#srcservicesidentity)
- [`src/Services/Diagnostics`](#srcservicesdiagnostics)
- [`src/Services/Theming`](#srcservicestheming)
- [`src/Services/Platform`](#srcservicesplatform)
- [`src/Services/SystemMetrics`](#srcservicessystemmetrics)
- [`src/Services/Network`](#srcservicesnetwork)
- [`src/Shell`](#srcshell)
- [`src/Shell/TrayNotice`](#srcshelltraynotice)
- [`src/Shell/Navigation`](#srcshellnavigation)
- [`src/Tabs/Dashboard`](#srctabsdashboard)
- [`src/Tabs/Toolkit`](#srctabstoolkit)
- [`src/Tabs/Settings`](#srctabssettings)
- [`src/Tabs/FileExplorer`](#srctabsfileexplorer)
- [`src/Tabs/Network`](#srctabsnetwork)
- [`src/Tabs/Hardware`](#srctabshardware)
- [`src/Tabs/Performance`](#srctabsperformance)
- [`src/Tabs/Storage`](#srctabsstorage)
- [`src/Tabs/Processes`](#srctabsprocesses)


## Bootstrap

```
/DashDetective
  Program.cs, App.axaml(.cs), app.manifest, Assets/   (bootstrap — project root)
  /src
```

## `src/Shared`

```
    /Shared                     (cross-cutting, feature-agnostic)
      ViewModelBase.cs
      ISelfScrollingPage.cs   (marker: a page that fills the viewport and scrolls its own panes, so
                               the shell must NOT wrap it in a scroll region — see File Explorer)
      IRefreshablePage.cs     (marker: a page the toolbar Refresh routes to; Refresh() re-reads its
                               data — Dashboard re-samples, File Explorer reloads the current folder)
      ILiveSamplingPage.cs    (marker: a page with live sampling the toolbar Live pill pauses/resumes;
                               MainWindowViewModel.ToggleLive routes SetLive() over every nav page)
      IActivatablePage.cs     (marker: a page that should only work while it is on screen.
                               MainWindowViewModel.UpdatePageActivity routes SetActive() over every nav
                               page — the current one when the window is visible, nothing otherwise, so
                               hiding to the tray idles the app rather than merely hiding it. The five
                               sampling pages implement it; Hardware/Toolkit/Settings/File Explorer own
                               no timer and do not)
      IReorderablePage.cs     (marker: a page whose widget order the user drags and the shell
                               persists. The page only holds the order; the shell reads and writes it)
      SamplingGate.cs         (composes ILiveSamplingPage's answer with IActivatablePage's into the one
                               a page's timers care about, so the five do not each hand-roll the pair.
                               STARTS live BUT NOT ACTIVE — this is what makes a tab that is never
                               opened cost nothing, and why page constructors must build their timers
                               STOPPED. Fires its callback only on a TRANSITION, so a re-selected tab or
                               a pill toggled off-screen cannot churn the timers)
      HardwareNameFormatter.cs (static: trims vendor/marketing decoration from CPU/GPU names for the
                                compact captions; shared by Dashboard + Performance. Distinct from
                                HardwareCatalog.Normalize — display trim, not a lookup key)
      CollectionReconciler.cs  (generic keyed diff of an ordered snapshot into an ObservableCollection —
                                drop/update/move/insert in place, no flicker; shared by the Network
                                connections table + the Processes list)
      EnumListCodec.cs         (a list of enum values as one persistable string, stored BY NAME so a
                                release that inserts a member cannot re-point a saved record. Reached by
                                the Processes column order and its collapsed sections)
      TrayIntegration.cs       (whether closing may hide to a tray icon rather than exit. WINDOWS ONLY:
                                stock GNOME runs no StatusNotifierItem host, and the setting is ON BY
                                DEFAULT, so honouring it there hides the window behind an icon that never
                                appears. Nothing can be asked at startup, and guessing wrong strands the
                                app — read by MainWindowViewModel.ShowInTray and the Settings toggle.
                                The FIRST hide additionally shows the tray notice — see /Shell/TrayNotice)
      AppInfo.cs               (product name + the entry assembly's informational version, so the
                                Settings footer reports the build it came from rather than a literal)
      Placeholders.cs          (the strings a surface shows with no real value: NoReading ("—"),
                                Unknown and the named Unknown* set. DISPLAY wording, NOT sentinels —
                                a reader still reports null/0 honestly and the consumer picks the
                                wording, because it differs (Processes wants "Unknown", Network
                                "PID 1234"))
      OverlapGuard.cs          (refuses a second poll while one is in flight; three pages had
                                hand-rolled the same bool-and-finally. LAST-WRITE-LOSES on purpose,
                                which is right for a timer and wrong for user-driven work — File
                                Explorer's generation counter and Toolkit's AllowConcurrentExecutions
                                are different answers, not copies. UI-thread-affine, so not
                                thread-safe; NvidiaSmiReader locks instead because it is not)
      PathComparison.cs        (how to tell whether two strings name the same path — Windows folds
                                case, Linux does not. IDENTITY ONLY: sorting and filtering stay
                                OrdinalIgnoreCase everywhere, since those are presentation)
      SystemDrive.cs           (where the OS is installed, resolved once. TWO SHAPES because the
                                platforms name it differently: Letter for keying a volume record,
                                Root for anything that opens or measures it)
      DataRateFormatter.cs     (network rates by magnitude (kbps/Mbps/Gbps, decimal base to match
                                Task Manager). Callers showing related values on one axis pick a
                                single unit from the shared peak via UnitFor)
      UptimeFormatter.cs       ("Nd Nh Nm" with leading zero units dropped)
      ClockFormat.cs           (24-hour / 12-hour, the persisted clock preference)
      TimeOfDayFormatter.cs    (on-screen wall-clock times under that preference. Invariant on BOTH
      FileSave.cs              (the ONE save-file flow: offer the formats, take a destination from the
                                native dialog, write what the chosen extension asked for. Replaced three
                                near-identical copies — toolbar Export, the two Settings buttons, the
                                Toolkit log — which is what made adding a format a three-place edit.
                                Content is rendered only after a destination is picked, and only in the
                                one format chosen)
                                arms: the 12-hour one must say AM/PM rather than whatever the host
                                locale designates. Display only — export names, the report's
                                "Generated" line and the app log stay 24-hour so files stay sortable)
      MemorySpeed.cs           (which of Win32_PhysicalMemory's two speeds to show: the CONFIGURED
                                clock (what Task Manager shows), falling back to the rated one. Two
                                tabs read the same modules and used to describe a stick two ways)
      PointerDrag.cs           (the movement threshold before a press counts as a drag, so a click
                                never nudges anything. Shared by NavigationView + the widget board)
      RevealFlash.cs           (tints an element when something elsewhere jumps to it. Toggles the
                                class only; the fade is Border.revealFlash's transition)
```

## `src/Shared/Charts`

```
      /Charts
        MetricHistory.cs        (THE rolling buffer type: a double[window] plus HOW MUCH OF IT IS REAL, and
                                 the canonical shift (left by one, append at the end) that MetricChannel
                                 used to hold. Every page's histories and MetricChannel's own are these —
                                 do not declare a bare double[]. The fill count is what lets Points() plot
                                 only the samples taken, so a trace enters at the right edge and grows
                                 leftward instead of drawing a zero-filled buffer as measured idle)
        SparklinePoints.cs      (renders a rolling metric history to a Sparkline "x,y" points string on a
                                 fixed 0–100 axis; percentage metrics pass valueMax 100, unbounded ones a
                                 rolling peak. The `filled` overload emits only the newest slots AT THEIR
                                 REAL INDICES — Sparkline takes its x scale from the data's own maximum, so
                                 that alone right-anchors a partial trace. Reached through
                                 MetricHistory.Points, which supplies the count)
        ChartScale.cs           (peak/headroom/floor auto-scaling for the network throughput axis —
                                 Peak / FitPeak / FitAxis; shared by Dashboard + Performance + Network.
                                 Takes ReadOnlySpan<double>, so callers pass MetricHistory.Values)
        ChartAxis.cs            (where a chart's axis text sits + what an auto-scaled axis says. Gutter /
                                 Footer RESERVE NOTHING when there are no labels, which is what keeps every
                                 unlabelled chart measuring as it did before axis text existed; PlotRect
                                 gives a reservation up rather than inverting on a control too small for it;
                                 GridLine snaps a grid line to the half-pixel AND clamps it inside the plot
                                 — an edge line drew half outside a chart that does not clip, bleeding into
                                 the padding of whatever hosted it; RateLabels builds the three value labels
                                 for a throughput axis. Pure geometry: text is MEASURED by the control,
                                 which alone has the typeface, and COMPOSED here, which is what keeps the
                                 layout rules testable with no render backend)
        ChartStatus.cs          (the cold-start wording, one place for the four pages that show it. Clears
                                 as soon as a chart has TWO samples — enough to draw a line — not when the
                                 window finally fills: a trace growing in from the right already says data
                                 is arriving, and the label sits on the plot it would be describing)
```

## `src/Shared/Styles`

```
      /Styles
        Palette.axaml           (colour brushes; merged in App.axaml. Light/Dark live in
                                 ResourceDictionary.ThemeDictionaries; accent + chart-series keys
                                 sit top-level and are swapped at runtime — see Theming below)
        SharedStyles.axaml      (reusable class styles: card, panel, seg, toggle, buttons,
                                 paneSplitter, revealFlash (the cross-tab reveal tint + its fade),
                                 tileLabel/tileValue, card.selectable…)
        Dimensions.axaml        (layout tokens: spacing, insets, radii, control heights. Theme-invariant,
                                 so always {StaticResource}. A token with no call site should not exist)
        Widgets.axaml           (the WidgetPanel and WidgetTable templates — TemplateBinding throughout,
                                 which is what satisfies compiled bindings without an x:DataType)
```

## `src/Shared/Layout`

```
      /Layout
        WidgetBoard.cs          (a page's widgets as one flow: packs rows to fit, caps each widget's width
                                 so surplus buys a column, and drags by the header to reorder)
        WidgetBoardLayout.cs    (its arithmetic + DropIndex — no Avalonia types, so it tests without layout)
        WidgetOrders.cs         (per-page order codec; the resolver that survives a widget being added,
                                 removed or renamed now lives in OrderResolver.cs, which this delegates
                                 to — the Processes columns want the same semantics)
        OrderResolver.cs        (that resolver, knowing only ids: drop what is gone, keep what is new
                                 beside the neighbours its author put it next to)
        UniformFlowPanel, FlowLayout, GridColumns, TableColumns, WeightedRowLayout
```

## `src/Shared/Controls`

```
      /Controls
        WidgetPanel, WidgetTable
                                       (WidgetPanel is one widget: surface, header row, body. Title /
                                        Subtitle / HeaderLead / HeaderContent / WidgetId. A surviving
                                        Border Classes="panel" is a SURFACE, not a widget.
                                        WidgetTable is a table's chrome: header above a scrolling body,
                                        one gutter for both. Columns and sorting stay at the call site)
        Sparkline, StatCard, ChartLegend, InfoRow
                                       (reusable widgets; Sparkline auto-fits to its data
                                        by default, or set YMin/YMax for a fixed axis —
                                        StatCard forwards YMin/YMax to its inner sparkline.
                                        Fixed-axis mode also supports an optional second series
                                        (Points2/Stroke2) + gradient area fill (Fill), used by the
                                        Network throughput panel for download+upload on one scale,
                                        a background lattice (ShowGrid), and OPT-IN AXIS FURNITURE:
                                        AxisMaxLabel/AxisMidLabel/AxisMinLabel down the left,
                                        AxisStartLabel/AxisEndLabel along the bottom, and StatusText
                                        over the plot. Each reserves room ONLY when set, so adding
                                        them to one chart cannot resize another. Render works against
                                        a plot Rect from ChartAxis and draws the grid + labels BEFORE
                                        the has-enough-points bail-out, so an empty chart still states
                                        its scale. All of this is fixed-range mode only.
                                        ChartLegend is a chart's key: up to two series, each a swatch
                                        in its own colour beside its name; an entry with no label takes
                                        no room, so the same control serves a single-series chart.
                                        InfoRow is a key/value row; long values wrap to multiple
                                        lines (flush-right) instead of clipping — see SharedStyles infoVal.
                                        Its Mono and Flush variants back the Network tab's IP config)
```

## `src/Services/Settings`

```
      /Settings
        AppSettings.cs          (immutable persisted-preferences record + Defaults; schemaVersion field)
        SettingsStore.cs        (load-on-start soft-fail to defaults; debounced atomic save to
                                (Load MERGES the file over AppSettings.Defaults key by key — LOAD-BEARING,
                                 not belt-and-braces. Deserializing directly discards every non-default
                                 initializer the file omits: the source generator treats a record's init
                                 properties as constructor parameters, builds the object from one args
                                 array, and fills absent slots with default(T). That silently loaded
                                 ShowInTray as false, and every alert threshold as 0 — which is OFF)
                                 %AppData%/DashDetective/settings.json; Flush on shutdown. Pure
                                 persistence — knows no view-models; the composition root applies/observes)
        SettingsJsonContext.cs  (System.Text.Json source-gen context for AppSettings; string enums)
```

## `src/Services/Startup`

```
      /Startup
        IStartupRegistration.cs (the seam + ForCurrentPlatform(); see Provider seams below)
        WindowsStartupRegistration.cs
                                (HKCU …\Run add/remove for "Launch at startup"; Microsoft.Win32.Registry,
                                 soft-failing. Holds UnsupportedStartupRegistration too — reports
                                 "not enabled" and ignores writes off Windows)
        LinuxStartupRegistration.cs
                                (the XDG counterpart: ~/.config/autostart/DashDetective.desktop, honouring
                                 XDG_CONFIG_HOME when it is absolute. IsEnabled READS THE FILE, not just
                                 its existence — the spec disables an entry with Hidden=true, and some
                                 desktop tools write that instead of deleting)
        DesktopEntry.cs         (the .desktop body as pure statics. Exec is QUOTED and its four reserved
                                 characters escaped: unquoted, a path under /home/My User/ parses as two
                                 arguments and launches the wrong thing)
```

## `src/Services/Identity`

```
      /Identity
        CurrentUserProvider.cs  (the interactive user's login name, initials badge and real privilege
                                 level, read once. Every source degrades independently — a denied token
                                 read reports the neutral "User" rather than guessing "Standard User",
                                 which would be a near-miss)
        IUserPictureProvider.cs (the seam + ForCurrentPlatform(); see Provider seams below. Returns the
                                 file's ENCODED bytes, not a decoded image, so it holds no UI type and is
                                 testable without a render backend)
        WindowsUserPictureProvider.cs
                                (the account picture: the AccountPicture\Users\{SID} registry index
                                 first, since that is what Windows itself maintains, then the tiles in
                                 %PUBLIC%\AccountPictures\{SID} for a stale or missing index. Tries 448
                                 downward, NOT the 1080 Windows also stores — the avatar is 32px. Holds
                                 UnsupportedUserPictureProvider too. Reads only; nothing here writes)
        LinuxUserPictureProvider.cs
                                (~/.face, then ~/.face.icon, then the AccountsService icon cached per user
                                 name. HOME FIRST because AccountsService holds a display-manager copy
                                 that can be older than what the user last set)
        UserPictureFile.cs      (the read both arms share: an 8 MB cap and one soft-fail, so a picture
                                 found through the registry and one at ~/.face obey identical rules)
```

## `src/Services/Diagnostics`

```
      /Diagnostics
        Log.cs                  (minimal soft-failing logger → Debug output + a per-day rolling file in
                                 %LocalAppData%/DashDetective/logs; never throws. The sampler / provider /
                                 MetricChannel catch blocks route through Log.Warn, and Program.cs hooks
                                 AppDomain.UnhandledException + TaskScheduler.UnobservedTaskException →
                                 Log.Error. No logging packages)
        DiagnosticsReport.cs    (the report as DATA — sections of key/value rows — rather than as text.
                                 It used to be built by string concatenation split across the shell and
                                 the Dashboard, which is why there was only ever one format)
        DiagnosticsFormat.cs    (the export formats + what each saves as, and the one place a report is
                                 rendered into one. FromFileName reads the format off the CHOSEN NAME:
                                 Avalonia does not report which picker filter was used, and a typed
                                 extension should beat a selected one)
        ReportFormatters.cs     (text / Markdown / HTML / CSV renderers. The TEXT one is pinned byte for
                                 byte by a test — a saved report and a new one still have to diff
                                 cleanly. Each escapes what its own syntax reserves, which is not
                                 hypothetical: every DNS list holds a comma and a machine name is
                                 user-controlled text. EXEMPT from the palette-ownership rule, since an
                                 exported page is a browser document with no access to the theme)
        DiagnosticsJsonContext.cs
                                (source-gen context for the JSON export, like SettingsJsonContext — it
                                 is what keeps the trimming/AOT gate clean)
```

## `src/Services/Theming`

```
      /Theming
        ThemeService.cs         (single seam that applies theme + accent to Application at runtime. Also
                                 the brush seam for a page that assigns colours in code rather than through
                                 {DynamicResource} — BrushFor(ChartSeries), cached per palette, plus a
                                 SeriesChanged event so that page can re-resolve. Only the Performance tab
                                 needs it; everything else binds the resource keys)
        AppTheme.cs             (enum: System / Light / Dark)
        AccentPreset.cs         (record: one accent's Color/Hover/OnAccent/Deep; .All = the four)
        ChartPalette.cs         (THE source of every chart series colour, for the default look and for each
                                 accent, plus the ChartSeries enum and the ChartSeriesColors record.
                                 An accent ROTATES the palette rather than flattening it: the accent is the
                                 CPU (and net-down) series, and every other series keeps its own saturation
                                 and lightness while its hue turns by the accent's offset from the default
                                 blue. Derive(AccentPreset.Default.Color) reproduces Default exactly, which
                                 is what makes the blue swatch and the "Default" swatch agree. Pure HSL
                                 maths over Avalonia.Media value types — no render backend, so it is
                                 unit-testable)
```

## `src/Services/Platform`

```
      /Platform
        /Linux
          IProcFileSystem.cs    (the /proc + /sys read seam — Exists/ReadAllText/ReadAllLines/
                                 ListDirectory/ResolveLink, all never-throwing and empty-on-miss.
                                 Infrastructure, not a provider seam, so it sits in its own Services
                                 folder like IUiTimer. ProcFileSystem is the real one; the tests' fake
                                 is what makes every Linux provider testable from a Windows box.
                                 See "/proc access" below for the Path.Combine rule)
          ProcFileSystem.cs
          ProcStatParser.cs     (/proc/stat cpu-line format knowledge, shared by the aggregate and
                                 per-core samplers: label + busy/total jiffies, read by index with a
                                 length check. steal counts as busy; guest/guest_nice are excluded
                                 because the kernel already folds them into user/nice)
          ProcMeminfoParser.cs  (/proc/meminfo format knowledge, shared by the memory sampler and the
                                 system-counters provider: "Key: value kB" → a byte-valued lookup. The
                                 kB unit is KIBIbytes, so suffixed values scale by 1024 (saturating);
                                 unitless values are counts and stay verbatim. Absent key → 0)
          ProcCpuinfoParser.cs  (/proc/cpuinfo format knowledge, shared by CpuFacts and the frequency
                                 sampler: one key/value block per logical processor, split on blank
                                 lines. Keys are trimmed around the colon — the file separates them
                                 with a VARYING number of tabs, so a fixed-column split passes a
                                 hand-written fixture and fails on a real machine. Keys are prose, so
                                 lookups are OrdinalIgnoreCase. Absent key → "")
          CpuFacts.cs           (the derivation on top of ProcCpuinfoParser, shared by the Dashboard's
                                 CPU tile and the Hardware tab's Processor card so the two cannot
                                 disagree: name, physical/logical cores, max clock. Physical cores =
                                 distinct (physical id, core id) PAIRS — block count over-reports on a
                                 hyperthreaded chip and core id alone merges sockets. Max clock prefers
                                 the highest cpuinfo_max_freq across cores (not cpu0's — that is a
                                 little core on big.LITTLE), else the model name's "@ 3.60GHz". cpu MHz
                                 IS NEVER USED for it: it is the instantaneous clock, so a scaling
                                 governor would report an idle 800 MHz under a "max" label. Reports ""
                                 and 0 honestly; each consumer applies its own placeholder.
                                 L3CacheKilobytes is a separate static, not a field — only the Hardware
                                 card has a row for it. Sysfs writes that size SUFFIXED ("8192K", "16M"),
                                 never as bytes)
          ProcPids.cs           (the live PIDs, from /proc's all-digit entries. A shared DERIVATION, not a
                                 parser: the Performance tab's process count and the Processes tab's full
                                 walk both start here, so what counts as a process is decided once. Empty
                                 means the listing failed, never that the machine is idle)
          ProcPidStatParser.cs  (/proc/[pid]/stat format knowledge — NOT ProcStatParser, which is the
                                 machine-wide /proc/stat. Yields comm, state char, parent PID, utime+stime
                                 and num_threads, so one open covers most of a process row. THE comm FIELD
                                 IS PARENTHESISED AND MAY HOLD SPACES AND PARENTHESES — "(Web Content)",
                                 "(a (b) c)" — so it splits on the LAST ')'. A whole-line Split(' ') lands
                                 on the wrong token for every field after the name, and does it for exactly
                                 the processes users care about. Read by index behind an 18-token minimum;
                                 anything shorter is a torn read and the process is skipped)
          ProcPidStatusParser.cs
                                (/proc/[pid]/status, for the two fields stat cannot supply: the real uid and
                                 VmRSS. PPid, Threads and State are in this file too and are DELIBERATELY
                                 NOT read — stat is already open, and one number should have one source.
                                 The Uid line carries FOUR values (real, effective, saved-set, filesystem)
                                 and only the first is the owner. An unknown uid is null, NOT 0: 0 is root,
                                 and a denied read must never promote a user process into the System group.
                                 A missing VmRSS is 0 bytes — a kernel thread has no address space, and
                                 requiring the field would drop every kworker from the list)
          ProcPidIoParser.cs    (/proc/[pid]/io, for the Disk column. rchar + wchar, NOT read_bytes +
                                 write_bytes: the Windows column is ReadTransferCount + WriteTransferCount,
                                 which counts bytes through the syscall layer including cache, and rchar/
                                 wchar are that same measurement. Mode 0400 — readable for your own
                                 processes, denied for root's and other users', which is a blank rate)
          ProcCgroupParser.cs   (/proc/[pid]/cgroup, the input to LinuxProcessClassifier. Every line is
                                 hierarchy-ID:controllers:path and the unified v2 hierarchy is the one with
                                 ID 0 AND AN EMPTY CONTROLLER LIST — a hybrid v1/v2 host lists a dozen v1
                                 controllers alongside it, so taking the first or last line yields a v1
                                 path. Leaf() splits the last segment, which two classifier rules match on.
                                 A v1-only host has no unified line at all and yields "")
          ProcMountsParser.cs   (/proc/mounts format knowledge, used by LinuxVolumeProvider: space-separated
                                 device / mountpoint / fstype, read by index with a length check. The device
                                 and mount-point fields are OCTAL-ESCAPED (\040 = space, \134 = backslash),
                                 because the separator is a space — read raw, a literal "\040" shows up in
                                 the Partitions table for most removable media. A malformed escape is left
                                 as written. UnescapeUdev handles the DIFFERENT \xNN hex convention the
                                 /dev/disk/by-label symlink names use for the same job)
          ProcDiskstatsParser.cs
                                (/proc/diskstats format knowledge, used by the throughput sampler. Keyed by
                                 the PACKED major:minor of columns 1-2 — the same identity SysBlockFacts
                                 derives, which is what lets an independently-ticking sampler agree with the
                                 drive cards. Read by index behind a 14-FIELD MINIMUM: the row grew to 18
                                 fields in 4.18 (discards) and 20 in 5.5 (flushes), so only the first
                                 fourteen may be assumed. Sectors are 512 bytes, as in /sys/block/*/size)
          SysBlockFacts.cs      (the /sys/block derivation, shared by the Storage tab's drive cards, its
                                 Partitions table and the Hardware tab's Storage Devices card — the CpuFacts
                                 shape applied to disks. THE JOIN KEY FOR THE WHOLE STORAGE SURFACE is
                                 Pack(major, minor) = (major << 20) | minor, read from /sys/block/*/dev:
                                 PhysicalDiskInfo, VolumeInfo and DiskThroughputSample are all keyed by an
                                 int disk number that Windows gets from the OS, and Linux has no such
                                 number, so three independently-sampled providers derive one from the
                                 kernel's own device identity. A positional index would drift the moment a
                                 USB drive is plugged in mid-run. FILTERS loop*/ram*/zram*/sr* — a stock
                                 Ubuntu GNOME install has ~25 snap loop devices and without this the Storage
                                 tab is unusable. dm-*/md* are RESOLVED, NOT DROPPED: anything with a
                                 slaves/ entry is followed (to a depth cap, for LUKS-over-LVM) to the disk
                                 backing it, so an LVM root still lands on a real card. Partition→disk needs
                                 no symlink resolution — the kernel nests partitions inside their disk and
                                 prefixes them with its name. size is in 512-BYTE SECTORS regardless of the
                                 drive's physical sector size. DiskNumbers() is the cheap half, for the
                                 sampler, which runs every tick and only needs to tell a disk from a
                                 partition; DiskNumberOf() is the single-read name→number path, for a
                                 caller that already knows the device name — WHOLE DEVICES ONLY, since a
                                 partition's dev file carries the partition's number)
          DrmCardFacts.cs       (the /sys/class/drm derivation, shared by the adapter enumeration, the
                                 utilisation sampler, the sensor reader and the Hardware Graphics card —
                                 the CpuFacts shape applied to GPUs. THE JOIN KEY FOR THE WHOLE GPU SURFACE
                                 is Key = the card's PCI ADDRESS, standing in for a DXGI LUID: the inventory
                                 keeps only adapters the enumeration AND the sampler both report, so two
                                 readers deriving keys separately means no GPU card at all, silently.
                                 COUNTS cardN ONLY — /sys/class/drm mixes cards with renderD* nodes and one
                                 entry per connector (card0-DP-1), and counting those turns one GPU into
                                 four. SKIPS a node with no PCI vendor: no ids, no name, nothing to show.
                                 PCI id files carry an 0x PREFIX that NumberStyles.HexNumber REJECTS — miss
                                 the strip and every id reads 0. IsSoftware flags ONLY simpledrm/vkms: unlike
                                 DXGI's flag, a paravirtualised GPU IS the VM's real display adapter and
                                 hiding it leaves the VM with no GPU card at all. Bundled vendor table +
                                 driver name gives "AMD amdgpu (1002:73df)", degrading to raw hex)
          ProcNetParser.cs      (/proc/net/{tcp,tcp6,udp,udp6} format knowledge, used by
                                 LinuxConnectionsInterop. ONE parser for all four files: they share the ten
                                 leading columns (sl, local, remote, st, queues, timer, retrnsmt, uid,
                                 timeout, inode), so only the trailer differs. ADDRESSES ARE HEX 32-BIT
                                 WORDS IN HOST BYTE ORDER, NOT NETWORK ORDER — 0100007F is 127.0.0.1, and
                                 an IPv6 address is FOUR such words reversed INDEPENDENTLY; reversing all
                                 sixteen bytes instead puts a ::ffff: marker at the wrong end and yields a
                                 plausible but wrong global address. Ports sit in the same field and must
                                 NOT be swapped. State passes through as the KERNEL's code — translation is
                                 the interop's job. The sl header survives a column count (twelve fields)
                                 and is dropped by failing to decode as an address)
          SocketInodeMap.cs     (socket inode → owning PID by walking /proc/[pid]/fd for socket:[N] — the
                                 only rootless attribution, since /proc/net names an inode and never a PID.
                                 STATEFUL AND CACHED on purpose: the walk is a readlink per descriptor
                                 across every process and the connections table polls at 2.5 s, so it only
                                 walks when asked about an unseen inode. A SHARED SOCKET RESOLVES TO THE
                                 LOWEST PID, not the first one walked — /proc listing order is unspecified,
                                 and the row's identity key carries the PID, so an unstable choice would
                                 break the UI's keyed diff. Reads the inode by locating the socket:[ marker
                                 ANYWHERE in the link target, because ResolveLink returns a full path
                                 (/proc/1/fd/socket:[N]) rather than the bare target. An unlistable fd
                                 directory is the natural other-user filter, at one call per process)
          ProcPidName.cs        (the cmdline→comm name derivation, shared by the Processes tab and the
                                 Network tab's connection owners — the CpuFacts shape applied to names, so
                                 the same process cannot read systemd-resolved on one tab and the 15-char
                                 truncated systemd-resolve on the other. cmdline holds NUL-separated args,
                                 so argv[0] ends at the first NUL. Reports "" for a process that names
                                 itself nowhere, because the consumers' placeholders differ — Processes
                                 wants "Unknown", Network wants "PID 1234". No .exe is appended)
          IVolumeCapacityReader.cs
                                (a mounted filesystem's total/free bytes by mount point, over DriveInfo —
                                 the managed statvfs. A seam of its own beside IProcFileSystem for the same
                                 reason: /proc/mounts carries no sizes and statvfs is not a pseudo-file, so
                                 without it LinuxVolumeProvider could not be tested until someone ran the
                                 VM. Free is TotalFreeSpace, not AvailableFreeSpace — only that makes the
                                 cards' used figure agree with df, and it is what the WMI arm's
                                 SizeRemaining means)
          OsReleaseParser.cs    (/etc/os-release format knowledge: KEY=value into a lookup. The file is
                                 a shell fragment, so the same body mixes quoted and bare values — one
                                 MATCHED pair of surrounding quotes is stripped and an unbalanced one is
                                 left alone. Splits on the first = only. Absent key → "")
          DmiIdReader.cs        (the one-line files under /sys/class/dmi/id, shared by the Dashboard's
                                 System Information panel and the Hardware tab's Motherboard card.
                                 EXPOSES ONLY THE WORLD-READABLE KEYS as named properties —
                                 product_uuid, board_serial and product_serial are mode 0400 and are
                                 deliberately not offered, so no caller can depend on a value that
                                 silently reads "" for every non-root user. Carries Join (the DMI
                                 counterpart to WmiRead.Join) and Year, which reads SMBIOS's MM/DD/YYYY
                                 from the END — the opposite side from WmiRead.DmtfYear's yyyymmdd)
```

## `src/Services/SystemMetrics`

```
      /SystemMetrics
        IProcessorFrequencySampler.cs (seam + ForCurrentPlatform() + the ProcessorClockSample record,
                                 which carries EITHER a ratio or an absolute clock — Windows PDH reports
                                 % of base clock, Linux reports MHz directly and has no dependable base
                                 to divide by. Page-local to the Performance tab's CPU Speed tile)
        WindowsProcessorFrequencySampler.cs
                                (live CPU current-clock ratio via PDH
                                 \Processor Information(_Total)\% Processor Performance — × the WMI base
                                 clock, like Task Manager. NOT the % Processor Utility counter, which is
                                 utilisation, not clock speed. Holds UnsupportedProcessorFrequencySampler)
        LinuxProcessorFrequencySampler.cs
                                (cpufreq scaling_cur_freq averaged over the online cores, falling back to
                                 /proc/cpuinfo's cpu MHz — which is what a VM usually has, since cpufreq
                                 is typically absent under VirtualBox)
        ILogicalProcessorSampler.cs (seam + ForCurrentPlatform() + the LogicalProcessorSample record;
                                 per-logical-processor % for the Performance tab's CPU "Detailed" view)
        WindowsLogicalProcessorSampler.cs
                                (per-core % via the PDH \Processor Information(*)\% Processor Utility
                                 array, _Total roll-ups dropped. Holds UnsupportedLogicalProcessorSampler)
        LinuxLogicalProcessorSampler.cs
                                (per-core % from /proc/stat's cpu0..cpuN. /proc/stat lists ONLINE cpus
                                 only, so state is keyed by core number and a core appearing mid-run
                                 reports 0 until it has an interval — never its since-boot average)
        ICpuSampler.cs          (seam over the total-CPU readers + UnsupportedCpuSampler)
        CpuUsageSampler.cs      (live total CPU %; its public ctor is the ONE place the CPU reader's
                                 platform is chosen — LinuxCpuSampler, else the PDH-then-GetSystemTimes
                                 chain on Windows, else Unsupported)
        LinuxCpuSampler.cs      (total CPU % from /proc/stat's aggregate line. STATEFUL — holds the
                                 previous jiffy snapshot, so it must not go into HardwareProviders)
        IMemoryUsageSampler.cs  (seam + ForCurrentPlatform() + the MemorySample record; live RAM % +
                                 used/total + the commit pair, feeding the shared Memory metric)
        WindowsMemoryUsageSampler.cs
                                (GlobalMemoryStatusEx. Latches inert in Sample() rather than a ctor, since
                                 there is no query to stand up. Holds UnsupportedMemoryUsageSampler)
        LinuxMemoryUsageSampler.cs
                                (/proc/meminfo; used = MemTotal − MemAvailable, the closest analogue to
                                 the Windows load figure and what `free -h` calls available. Falls back to
                                 MemFree + Cached + Buffers on pre-3.14 kernels. Committed_AS/CommitLimit
                                 pass through UNCLAMPED — overcommit legitimately exceeds the limit)
        ISystemPerformanceProvider.cs
                                (seam + ForCurrentPlatform() + the SystemPerformanceSample record: file
                                 cache + process/thread/handle totals, shared by the Performance tab's CPU
                                 and Memory panes. EVERY member is nullable — a platform with no analogue
                                 reports null, which the tiles render "—")
        WindowsSystemPerformanceProvider.cs
                                (psapi GetPerformanceInfo — all four figures from one call, no PDH counter
                                 and no per-tick process enumeration. Holds
                                 UnsupportedSystemPerformanceProvider)
        LinuxSystemPerformanceProvider.cs
                                (cache = Cached + SReclaimable; threads = /proc/loadavg's nr_threads (the
                                 DENOMINATOR of field 4 — it is threads, not processes); processes =
                                 /proc's numeric entries, one listing and no per-PID opens. HANDLES ARE
                                 PERMANENTLY "—": a Windows handle covers events, threads and registry keys
                                 too, so /proc/sys/fs/file-nr would mean something else under the label)
        IGpuUsageSampler.cs     (seam: SampleAdapters() only. ITS KEYS MUST MATCH IGpuAdapterProvider'S —
                                 DeviceInventory intersects the two, so an arm that derives the adapter key
                                 differently from its enumeration counterpart yields NO GPU AT ALL rather
                                 than a wrong one, with every individual reading still looking fine. Also
                                 carries NvidiaMetricsEnabled as a DEFAULT interface member (get => false),
                                 so only the one arm with a spawn-costing source pays for the setting)
        WindowsGpuUsageSampler.cs
                                (live GPU % via PDH GPU Engine counters; owns a PDH query handle.
                                 SampleAdapters() = per-physical-GPU split keyed by adapter LUID token, and
                                 the whole surface: the combined Sample()/SampleEngines() pair the multi-GPU
                                 split replaced has been removed. Page-local per tab — the Dashboard cards +
                                 Performance rows each own one)
        LinuxGpuUsageSampler.cs (amdgpu gpu_busy_percent per card, keyed by the shared DrmCardFacts.Key.
                                 EVERY ADAPTER IS REPORTED, with a NULL Overall where the driver publishes
                                 no figure — omitting one would delete its card entirely, and a 0 would show
                                 real hardware as permanently idle. NO ENGINE BREAKDOWN: sysfs has one
                                 scalar per card and the per-engine split is root-only debugfs, so the
                                 Performance tab's Detailed toggle stays hidden. Card list resolved once at
                                 construction; only the utilisation file is re-read per tick)
        NvidiaSmiReader.cs      (the only rootless NVIDIA utilisation source — the proprietary driver
                                 publishes nothing in sysfs. SPAWNS A PROCESS, so it is off the sampling
                                 path entirely: at most one run per 15 s of WALL CLOCK (not per N ticks —
                                 the tick interval is a user setting), never overlapping, never blocking,
                                 gated behind an off-by-default setting, and retired for the session on the
                                 first failure. NORMALISES THE BUS ID: nvidia-smi writes an EIGHT-digit PCI
                                 domain and uppercase hex where sysfs writes four and lowercase, so a raw
                                 join matches nothing and every reading silently fails to find its card.
                                 Runs over the Toolkit's IProcessLauncher seam, so tests spawn nothing)
        HardwareProviders.cs    (the "what hardware is in this machine" bundle + the single
                                 ForCurrentPlatform() that picks the Windows, Linux or unsupported set
                                 for all SEVEN members. The Linux arm is now complete except for per-DIMM
                                 memory, which stays Unsupported* for good (dmidecode needs root).
                                 It carries NO
                                 [SupportedOSPlatform]: the Linux readers are portable managed code over
                                 IProcFileSystem, so there is no annotated API for CA1416 to see.
                                 Built by each consuming page's public ctor (Dashboard,
                                 Performance, Storage) and handed to DeviceInventory.LoadAsync.
                                 EVERY MEMBER MUST BE STATELESS — it is constructed three times and its
                                 members run concurrently; stateful providers are deliberately excluded)
        IGpuAdapterProvider.cs  (seam + the GpuPciId / GpuAdapter records. GpuAdapter.FormatLuidToken —
                                 pure, unit-tested — lives on the record, not the DXGI reader.
                                 GpuAdapter.LuidToken IS NAMED FOR WINDOWS BUT IS NOT ALWAYS A LUID: it is
                                 whatever this platform uses to say "the same adapter" across independent
                                 readers — the PDH luid token on Windows, the card's PCI address on Linux)
        LinuxGpuAdapterProvider.cs
                                (/sys/class/drm enumeration over the shared DrmCardFacts, taking its Key
                                 rather than deriving one. Packs the two sysfs subsystem id files into the
                                 single field DXGI reports, so a card reads the same on both platforms)
        WindowsGpuAdapterProvider.cs
                                (DXGI adapter enumeration via raw vtable fn-pointers — LUID→name + software
                                 flag + VRAM; the authoritative LUID→name map for multi-GPU, async. Its
                                 DedicatedVideoMemory rides through DeviceInventory onto
                                 DeviceInstance.VramBytes → the Performance GPU VRAM tile)
        IPhysicalDiskProvider.cs / WindowsPhysicalDiskProvider.cs
                                (all-disks WMI enumeration; takes IDiskTemperatureProvider by ctor —
                                 ForCurrentPlatform shares ONE reader with the Storage page)
        LinuxPhysicalDiskProvider.cs
                                (the same cards from SysBlockFacts, taking IDiskTemperatureProvider the same
                                 way. HEALTH IS ALWAYS HEALTHY — SMART needs root and has no rootless
                                 near-miss. Temperature IS read, and unlike the Windows arm it is asked for
                                 on EVERY drive, not just NVMe: the source is hwmon, and drivetemp covers
                                 SATA/SAS too, so a media-kind gate here would make that path dead code)
        IVolumeProvider.cs / WindowsVolumeProvider.cs
                                (MSFT_Volume enumeration incl. unlettered Recovery/EFI. VolumeInfo carries
                                 BOTH DriveLetter and MountPoint — the platforms name the same thing
                                 differently and callers fall through from one to the other; a Windows
                                 volume leaves MountPoint empty and a Linux one DriveLetter null)
        LinuxVolumeProvider.cs  (/proc/mounts joined to SysBlockFacts, sized through IVolumeCapacityReader.
                                 ONE FILTER RULE does the whole job: keep a mount only when its device
                                 resolves to a disk that has a card. tmpfs/cgroup/proc name no /dev device
                                 and every snap mount resolves to an excluded loop, so both floods fall out
                                 and no volume points at a cardless disk — a filesystem allowlist would be a
                                 weaker second guess. DEDUPES BY RESOLVED DEVICE, shortest mount point
                                 winning: /proc/mounts lists one device many times (bind mounts, btrfs
                                 subvolumes) and StorageComposer SUMS a disk's volumes, so duplicates
                                 multiply its capacity. /dev/mapper and /dev/disk/by-uuid names are
                                 symlinks and are resolved. Labels come from the by-label symlinks)
        SystemVolume.cs         (which volume hosts the OS, and through it which disk — the Dashboard's
                                 Storage tile and the Storage tab's Disk Activity panel both need it and
                                 each used to hold its own copy. Tries SystemDrive.Letter, then the "/"
                                 mount point; only one arm can ever match on a given platform)
        IDiskTemperatureProvider.cs / WindowsDiskTemperatureProvider.cs
                                (NVMe composite temp via non-admin IOCTL health log. SYNCHRONOUS by
                                 design — called per-disk on a slow sub-tick of a timer the caller owns)
        LinuxDiskTemperatureProvider.cs
                                (temp1_input from the drive's hwmon, in MILLIDEGREES. MATCHED ON THE
                                 HWMON'S name, NEVER ITS INDEX — numbering is not stable across boots and
                                 the low ones are usually coretemp/acpitz, so an index read reports the CPU
                                 on a drive card. TWO WALKS reach the block device: an nvme hwmon hangs off
                                 the CONTROLLER with the device a namespace child (nvme0 → nvme0n1), a
                                 drivetemp one off a SCSI target with the device under block/. FINDING
                                 NOTHING IS THE COMMON CASE — drivetemp is not loaded by default on most
                                 distros; that is correct, not a bug. Stateless: re-walks per call)
        IPhysicalDiskThroughputSampler.cs
                                (seam + the DiskThroughputSample record + ForCurrentPlatform(). Unlike the
                                 HardwareProviders members this is deliberately STATEFUL — every arm reports
                                 the interval since the previous call — so each page owns its own instance.
                                 Holds UnsupportedPhysicalDiskThroughputSampler's contract; the twin itself
                                 sits under the Windows arm)
        WindowsPhysicalDiskThroughputSampler.cs
                                (per-disk read/write/active/response/queue from the PDH \PhysicalDisk(*)
                                 counter ARRAY — deliberately not the _Total instance, whose % Idle Time is
                                 a mean across every disk. Holds the Unsupported twin)
        LinuxPhysicalDiskThroughputSampler.cs
                                (the same five figures from /proc/diskstats, diffed over a Stopwatch
                                 interval like NetworkUsageSampler. ACTIVE TIME COMES FROM io_ticks
                                 (milliseconds with a request outstanding) — the direct analogue of
                                 100 − % Idle Time, and what every headline number and sparkline on the
                                 Storage tab and the Dashboard's disk cards renders; without it they all
                                 read a flat zero. Response = Δ(ms reading + ms writing) over Δ completed
                                 transfers; queue depth is INSTANTANEOUS, not a delta. REPORTS WHOLE DISKS
                                 ONLY — /proc/diskstats lists sda and sda1 alike and their I/O overlaps, so
                                 counting both roughly doubles every figure. A counter that goes backwards
                                 (device re-plugged) reads as no activity)
        MetricChannel.cs        (reusable "sampler + DispatcherTimer + rolling MetricHistory"
                                 unit — one try/catch per tick → onFailed + permanent stop; SampleNow for
                                 paused Refresh. Non-generic MetricChannel for plain-double metrics,
                                 generic MetricChannel<TSample> for snapshot samples + a no-history variant)
        SystemMetricsService.cs (SINGLE owner of the three SHARED samplers — CPU, Memory, Network;
        ResourceAlert.cs        (AlertMetric + the breach being reported: resource, DEVICE NAME, reading,
                                 threshold. Named because "a GPU is busy" on a two-GPU machine is not
                                 actionable)
        ResourceAlertOptions.cs (the user's thresholds. ZERO MEANS OFF, which collapses "enabled" into the
                                 value — one control per row, one check in the watcher. GPU and disk
                                 activity default off: sustained saturation of either is what legitimate
                                 heavy work looks like)
        ResourceAlertWatcher.cs (watches CPU/memory (shared feeds) plus GPU, disk activity and free space
                                 against those thresholds. Owns its OWN GPU + disk samplers, which their
                                 contracts require, and never aggregates — it takes the WORST device and
                                 names it, so the no-shared-aggregate rule above still holds. Sustain is
                                 SECONDS converted against the live interval, not a sample count, which
                                 used to mean 5 s at the 0.5 s cadence and 50 s at the 5 s one. Free space
                                 skips UNLETTERED volumes: Recovery/EFI sit near-full by design, so
                                 watching them is a banner that never goes away)
                                 per-metric 1 Hz channel fans each sample out to subscribers (ref-counted — a
                                 channel runs only while it has one), Pause/Resume for the Live pill,
                                 RefreshAll for Refresh, per-metric fault isolation. Dashboard / Performance /
                                 Processes SUBSCRIBE instead of owning these samplers. Per-GPU and per-disk
                                 readings are page-local instead (multi-instance; a shared aggregate feed would
                                 report a mean across every device under a label naming one of them), as is
                                 the Network tab's own NetworkUsageSampler. Built in
                                 the App composition root and disposed on shutdown.
                                 THE ALERT WATCHER IS OPT-IN (AlertsEnabled, mirroring the "Resource alerts"
                                 setting and off by default like it): it subscribes to CPU + Memory, so left
                                 on it holds both channels sampling with every page deactivated — which is
                                 most of what the app used to cost hidden in the tray. Clearing it also
                                 clears any active alert, so a banner cannot outlive its setting.)
        MetricSubscriptions.cs  (a page's subscriptions as FACTORIES rather than tokens, with idempotent
                                 Attach/Detach, so IActivatablePage can drop and re-establish them.
                                 DROPPING THEM IS WHAT STOPS THE FEED — the service ref-counts subscribers,
                                 so a deactivated page that merely ignored its callbacks would still be
                                 paying for them. Re-attaching replays the cached latest sample, so a page
                                 returning to screen seeds with real data instead of a blank frame)
```

## `src/Services/Network`

```
      /Network
        NetworkGateway.cs       (the machine's own IPv4 default gateway, over NetworkUsageSampler
                                 .SelectPrimary. Shared because two features want it: the Toolkit's
                                 parameterised ping/tracert boxes and the Network tab's ping panel.
                                 REPORTS null RATHER THAN A FALLBACK HOST — the two callers answer "no
                                 gateway" differently, and the lookup must not decide for them: the
                                 Toolkit substitutes a literal (a `ping <host>` row with an empty box is
                                 a dead button), while the Network tab leaves its box empty rather than
                                 offering a host the user never asked to contact. Never throws; slow
                                 enough that callers run it off the UI thread)
        NetworkUsageSampler.cs  (live down/up Mbps via managed NetworkInterface; samples ONE primary
                                 adapter — internet-facing, has a default gateway — NOT a sum of all
                                 adapters, see the gotcha below. Shared: Dashboard and the Network tab
                                 each own an instance, and the Network tab's AdapterInfoProvider reuses
                                 SelectPrimary() to identify the primary adapter — one source of truth.
                                 Moved here from src/Tabs/Dashboard with sign-off when the Network tab
                                 was activated.)
```

## `src/Shell`

```
    /Shell                      (the app frame — the "default window")
      MainWindow.axaml(.cs), MainWindowViewModel.cs, ViewLocator.cs
                                (MainWindow's root is a DockPanel hosting the NavigationView at the
                                 user-chosen edge (DockPanel.Dock bound to Nav.Dock) + the main area.
                                 MainWindow's page-host is a Panel with two mutually-exclusive hosts:
                                 a scrolling ScrollViewer (ScrollingPage) and a bounded ContentControl
                                 (SelfScrollingPage), so ISelfScrollingPage pages self-scroll within
                                 the viewport — see File Explorer)
```

## `src/Shell/TrayNotice`

```
      /TrayNotice
        TrayNoticeWindow.axaml(.cs) (the ONE-TIME "this app is still running" dialog, shown before the
                                     FIRST hide-to-tray and never again (AppSettings.TrayNoticeShown).
                                     Asked BEFORE hiding, over the window the user has just closed: a
                                     toast afterwards would have to guess where the tray is, and can be
                                     missed. MainWindow.OnClosing cancels the close and awaits it from a
                                     helper, the ExportReportAsync split, since a closing handler cannot
                                     await. AskAsync returns bool? ON PURPOSE — with a plain bool a
                                     title-bar dismissal is default(bool) and would EXIT the app; the
                                     safe answer is the one the setting already gives. No view model:
                                     it holds no state but which button was pressed)
```

## `src/Shell/Navigation`

```
      /Navigation
        NavigationView.axaml(.cs)   (the collapsible/dockable nav-bar component; brand + item list +
        NavigationViewModel.cs       footer, with no permanent control chrome — collapse is the hover
                                     puck, re-docking is the right-click menu or the drag gesture. The
                                     VM owns Orientation + IsCollapsed and exposes all layout as computed
                                     properties — Dock, Rail sizes, ItemsOrientation, Hairline edge,
                                     scroll axis, puck geometry — no converters. Selection/layout visuals
                                     are styled in NavigationView.axaml via DynamicResource so they
                                     follow theme + accent)
        NavItem.cs, Icons.cs        (NavItem is a pure data model; Icons holds the glyph geometries)
        NavOrientation.cs           (enum: the dock edge — Left/Right/Top/Bottom)
        ChevronDirection.cs         (enum: which way the puck's chevron points. Split from the geometry
                                     so the rule is testable — Geometry.Parse needs a render backend,
                                     which the unit tests do not have, so touching Icons at all throws)
        NavPositionOption.cs        (selectable item VM for the dock menu, like NavItem/ThemeOption)
```

## `src/Tabs/Dashboard`

```
      /Dashboard                DashboardView.axaml(.cs) + DashboardViewModel.cs
                                ICpuInfoProvider.cs + WindowsCpuInfoProvider.cs
                                                        (CPU info via WMI, async. Reached through the shared
                                                         HardwareProviders bundle, NOT statically)
                                LinuxCpuInfoProvider.cs (the same card from /proc/cpuinfo + cpufreq, via the
                                                         shared CpuFacts. Substitutes only the placeholder
                                                         name; physical cores and clock stay 0 → "—")
                                CpuStaticInfo.cs        (record for the result)
                                IMemoryInfoProvider.cs + WindowsMemoryInfoProvider.cs
                                                        (RAM info via WMI, async)
                                MemoryStaticInfo.cs     (record for the WMI result)
                                (the CPU/Memory/GPU/Storage/Network *samplers* now live under
                                 src/Services/SystemMetrics + /Network and are owned by
                                 SystemMetricsService — the Dashboard VM subscribes, it no longer owns them)
                                ISystemInfoProvider.cs + WindowsSystemInfoProvider.cs
                                                        (static system identity — OS/device/BIOS/board/build —
                                                         via WMI + registry, async; uptime is live off
                                                         Environment.TickCount64 in the VM, no sampler file)
                                LinuxSystemInfoProvider.cs
                                                        (the same panel from /etc/os-release (PRETTY_NAME, then
                                                         NAME+VERSION_ID, then OSDescription),
                                                         /proc/sys/kernel/osrelease for Build — the kernel
                                                         release is the analogue of the Windows build number —
                                                         and DmiIdReader for BIOS + board, which falls back to
                                                         the chassis fields laptops populate more reliably)
                                SystemStaticInfo.cs     (record for the system-identity result)
```

## `src/Tabs/Toolkit`

```
      /Toolkit                  ToolkitView.axaml(.cs) + ToolkitViewModel.cs
                                (designed as the "Commands" tab. Filter bar (search box + category
                                 chips + count) over a grouped command list, beside a pinned 340px
                                 Execution Log. ISelfScrollingPage (each column scrolls itself) +
                                 IShortcutTarget. Running a row is an async RelayCommand that refuses
                                 concurrent runs and prepends one stanza to the log. View code-behind
                                 owns the search focus + the search-reveal flash, like
                                 ProcessesView/SettingsView. The row is a chrome-less Button so Enter
                                 works on a focused row natively — ShortcutId.Activate falls through
                                 MainWindowViewModel's global switch unconsumed, so no IShortcutTarget
                                 case is needed. The copy button is a SIBLING of it, not nested)
                                ToolkitCategory.cs      (enum: the four sections, declaration order =
                                                         display order)
                                ToolkitEntryKind.cs     (enum: Folder / App / Command / Panel / Link —
                                                         what a command opens, driving its icon + badge)
                                ToolkitEntry.cs         (immutable row model; its Icon/Badge* getters
                                                         resolve through ToolkitIcons ON READ, so the
                                                         filter/catalog tests never load geometry)
                                ToolkitGroup.cs         (immutable category section: upper-cased header
                                                         + the entries that survived the filter)
                                ToolkitCategoryOption.cs (filter-chip item VM, the FilterOption shape)
                                ToolkitLogEntry.cs      (one console stanza in the Execution Log.
                                                         OBSERVABLE, and it keeps the raw Timestamp beside
                                                         the formatted Time: the clock-format preference can
                                                         change while rows are on screen, and a row storing
                                                         only its pre-formatted string could not be re-stamped)
                                ToolkitCatalog.cs       (static COPY table only — categories, section
                                                         headers, badge labels. Reads the same on every
                                                         platform, which is why it stayed static when the
                                                         command set did not)
                                IToolkitCatalog.cs      (the per-platform command set, three arms:
                                                         WindowsToolkitCatalog / LinuxToolkitCatalog /
                                                         UnsupportedToolkitCatalog. NO
                                                         [SupportedOSPlatform] on any of them — they are
                                                         string literals with no platform API surface,
                                                         and Instance is a static field initialised
                                                         outside the guard anyway)
                                WindowsToolkitCatalog.cs, LinuxToolkitCatalog.cs,
                                UnsupportedToolkitCatalog.cs
                                                        (the tables. Unsupported is EMPTY on purpose:
                                                         a platform with no table gets the page's own
                                                         empty state and can still author its own rows,
                                                         which beats thirty rows that can only fail.
                                                         EACH TABLE HAS EXACTLY ONE ELEVATED ROW —
                                                         sfc /scannow, fwupdmgr refresh — pinned by name
                                                         in that catalog's own tests, because a table is
                                                         the only place elevation can be authored at all)
                                ToolkitRows.cs          (the shared row factories — Folder/Tool/Panel/
                                                         Diagnostic/Doc. Both tables build through them,
                                                         so a category cannot drift away from the kind
                                                         and action it is supposed to pair with)
                                ToolkitActionKind.cs    (enum: OpenPath / OpenUrl / Launch / Capture /
                                                         Elevated — how a row is carried out. An enum,
                                                         not flags, so "elevated AND captured" cannot be
                                                         expressed: Windows forbids redirecting a runas
                                                         process's streams)
                                ToolkitAction.cs        (immutable: target + argument LIST + timeout,
                                                         built only via static factories. WithArgument
                                                         appends exactly one element, so a parameterised
                                                         entry's value can never split into a flag)
                                ToolkitRunner.cs        (THE single entry point for running a row. Never
                                                         throws — a missing tool, non-zero exit, timeout
                                                         or declined UAC prompt all become worded
                                                         failures. Expands env vars in the TARGET only;
                                                         refuses any OpenUrl that isn't https://)
                                ToolkitRunResult.cs     (record: Success / Output / ExitCode)
                                ToolkitOutputFormatter.cs (pure statics: stream merge, CRLF normalising,
                                                         console sign-off trim, 200-line / 16 KB caps with
                                                         the trim announced, plus the outcome wording)
                                IProcessLauncher.cs +   (the process seam + its real implementation — the
                                SystemProcessLauncher.cs only place in the app that starts a process.
                                                         Arguments go via ProcessStartInfo.ArgumentList,
                                                         never the joined string. Both output streams are
                                                         drained CONCURRENTLY and WITHOUT the timeout
                                                         token, or a command that floods its pipe
                                                         deadlocks and a killed one loses what it printed.
                                                         BuildLaunchInfo takes the platform as a parameter
                                                         so both elevation arms are testable from either
                                                         host: runas verb on Windows, pkexec as the file
                                                         name with the target as its first argument on
                                                         Linux — and UseShellExecute MUST be off there or
                                                         the launch goes to xdg-open, which cannot carry
                                                         arguments)
                                ToolkitFilter.cs        (pure statics: Matches (chip AND term, over the
                                                         command and its description) + Group (buckets
                                                         into catalog order, dropping emptied sections).
                                                         The ProcessFilter pattern)
                                ToolkitIcons.cs         (feature-local per-kind glyphs + fixed badge
                                                         tints, the HardwareIcons pattern)
```

## `src/Tabs/Settings`

```
      /Settings                 SettingsView.axaml(.cs) + SettingsViewModel.cs
                                                        (fully live: Appearance + Navigation + Monitoring
                                                         + Export & Data; view code-behind owns the
                                                         export save dialog + clipboard, like MainWindow)
                                ThemeOption.cs, AccentOption.cs, ClockFormatOption.cs,
                                IntervalOption.cs       (selectable item VMs for the Appearance +
                                                         refresh-interval controls, like NavItem)
                                NumericField.axaml(.cs) (a typed whole number with its unit beside it —
                                ShortcutCaptureBox.axaml(.cs)
                                                        (arms, then captures the next key press as a
                                                         binding. The SHELL SEES THE KEY FIRST — its
                                                         listener tunnels from the window — so it raises
                                                         CapturingChanged for the view model to hold, and
                                                         the shell stands down on it. Modifier-only
                                                         presses are ignored; Esc abandons)
                                ShortcutRow.cs          (one Keyboard-card row: the action, its keys,
                                                         whether it is custom, and the note explaining a
                                                         refused capture where it happened)
                                                         "90 %", "10 s". Digits only, filtered on a
                                                         TUNNELLED TextInput so a paste cannot smuggle a
                                                         letter past it. Takes the value AS IT IS TYPED,
                                                         not on focus loss: clicking anything that does
                                                         not take focus leaves the box focused, so a
                                                         commit-on-blur field silently lost the number.
                                                         Only the ceiling is enforced mid-edit — raising a
                                                         too-small number to the floor rewrites the box
                                                         under the caret — and the box is reconciled to
                                                         the stored value when the edit ends)
                                AlertThresholdRow.cs    (one Alerts row: IsEnabled + Value, kept APART so a
                                                         switched-off row remembers its number. The
                                                         settings layer encodes "not watched" as 0, which
                                                         alone would forget it, so GPU could not ship
                                                         "off, defaulted to 90")
                                GpuMetricsSupport.cs    (whether reading NVIDIA GPU utilization costs a
                                                         helper process here. LINUX ONLY: there the figure
                                                         exists solely through nvidia-smi, which is why the
                                                         setting is opt-in at all; Windows takes it from a PDH
                                                         counter it already polls, so the toggle has nothing to
                                                         turn on and the sampler discards the write. The
                                                         TrayIntegration shape — one named capability, read by
                                                         SettingDescriptions.NvidiaGpuMetricsFor and by
                                                         SettingsViewModel.CanUseNvidiaMetrics)
                                SettingDescriptions.cs  (the descriptions that name a MECHANISM rather
                                                         than an effect, or a platform that cannot honor
                                                         one, so cannot be shared — "Start with Windows",
                                                         the tray row's, and the NVIDIA row's. Each is the
                                                         label the page shows AND the text search matches,
                                                         so a wrong one is both misleading and unfindable.
                                                         Takes the platform as a parameter, the
                                                         ProcessGroupNames shape. KEYWORDS ARE NOT
                                                         per-platform: they are shared, so editing them
                                                         changes the Windows search index too. TWO rows are
                                                         DISABLED-NOT-HIDDEN on a platform that cannot
                                                         honor them (ShowInTray, NvidiaGpuMetrics): the
                                                         search index must not vary by platform, the search
                                                         reveal finds a row by its Tag IN THE VISUAL TREE
                                                         so a collapsed one is a dead hit, SettingCatalog
                                                         tests pin a catalog<->SettingId bijection, and a
                                                         settings.json carried between machines has to
                                                         survive. IsEnabled goes on the row BORDER, not the
                                                         toggle — a disabled toggle alone reads as an off
                                                         one)
```

## `src/Tabs/FileExplorer`

```
      /FileExplorer             FileExplorerView.axaml(.cs) + FileExplorerViewModel.cs
                                                        (VM implements ISelfScrollingPage +
                                                         IRefreshablePage; owns filter, sort + ShowHidden
                                                         state and RebuildVisibleEntries; drives live
                                                         auto-refresh + scroll-to-top-on-navigation)
                                DirectoryService.cs     (async System.IO enumeration: lazy subdirectories,
                                                         folder entries; per-entry soft-fail, Task.Run off
                                                         the UI thread; takes includeHidden to reveal
                                                         hidden/system entries. FileItem carries raw
                                                         Size/Modified sort keys. GetEntriesAsync takes
                                                         IShellInterop and GetDrivesAsync takes
                                                         IFileSystemRoots — both per-platform questions are
                                                         asked of a seam. Still static: it holds no state
                                                         and is in no bundle. RootHasChildren is the one
                                                         chevron probe both roots providers share.
                                                         GetEntriesAsync returns a FolderRead — the items
                                                         PLUS a FolderReadStatus saying why an empty one is
                                                         empty. IgnoreInaccessible suppresses the failure to
                                                         open the FOLDER ITSELF, not just its children, so a
                                                         denied folder is otherwise a successful empty list;
                                                         Diagnose re-asks WITHOUT the suppression and ONLY
                                                         when the listing produced nothing, which keeps the
                                                         partial-list contract and costs an ordinary folder
                                                         nothing. Do not just turn IgnoreInaccessible off)
                                FolderMessages.cs       (why the file list is blank, worded: title + hint for
                                                         denied / gone / unreadable / hidden-only / empty /
                                                         filtered-to-nothing / no-folder-open. Pure and
                                                         render-free BECAUSE FileExplorerViewModel cannot be
                                                         tested — the FileExplorerPanes shape)
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
                                FileTypeCatalog.cs      (extension → vector glyph + fixed colour. TOUCHES
                                                         AVALONIA — its Geometry.Parse initialiser needs a
                                                         render backend, so nothing testable may live here)
                                FileTypeDescriptions.cs (extension → friendly name, render-free precisely
                                                         because FileTypeCatalog is not. A table rather
                                                         than xdg-mime, which costs a subprocess per row.
                                                         Treats a leading dot as hidden, not an extension)
                                IShellInterop.cs        (seam + ForCurrentPlatform())
                                WindowsShellInterop.cs  (feature-local shell32 P/Invoke:
                                                         SHGetFileInfo type name + SHObjectProperties.
                                                         Holds UnsupportedShellInterop + the shared
                                                         ShellFallback. NOTE Open() stays live in BOTH —
                                                         it is managed Process.Start with UseShellExecute
                                                         and was never platform-guarded)
                                LinuxShellInterop.cs    (no interop at all: names from the table above,
                                                         Open via ShellFallback. NO PROPERTIES DIALOG
                                                         EXISTS — no desktop offers one to a foreign
                                                         process — so it reveals the containing folder
                                                         instead. RevealTarget is split from the launch so
                                                         it is testable without starting a file manager)
                                IFileSystemRoots.cs     (seam + ForCurrentPlatform(): what the tree's roots
                                                         are is the one per-platform question here)
                                WindowsFileSystemRoots.cs
                                                        (ready drives via DriveInfo, "Local Disk (C:)".
                                                         Holds UnsupportedFileSystemRoots — an EMPTY list,
                                                         which is what the old IsWindows() guard returned.
                                                         NO [SupportedOSPlatform]: DriveInfo, the
                                                         VolumeLabel getter and DriveType are unannotated,
                                                         so it would be decorative)
                                LinuxFileSystemRoots.cs (/, $HOME, and removable mounts from /proc/mounts
                                                         via ProcMountsParser. Matches the /media/,
                                                         /run/media/ and /mnt/ PREFIXES so no user name is
                                                         resolved — udisks2 uses the first on Ubuntu and
                                                         the second on Fedora/Arch)
```

## `src/Tabs/Network`

```
      /Network                  NetworkView.axaml(.cs) + NetworkViewModel.cs
                                                        (VM implements IRefreshablePage + ILiveSamplingPage
                                                         + IActivatablePage; it polls only while it is the
                                                         visible tab. Owns the throughput sampler +
                                                         adapter/connection/ping timers and the keyed-diff
                                                         for the connections list. THE PING AND DNS PANELS
                                                         ARE USER-INITIATED and start nothing on their own —
                                                         see the write-up in the Appendix. Tab-local
                                                         MonoFont + fixed console-colour resources live in
                                                         the view — promote to Shared if reused)
                                NetworkProviders.cs     (the tab's provider bundle + ForCurrentPlatform();
                                                         see Provider seams above. TWO platform choices here
                                                         — which IConnectionsInterop and which
                                                         IProcessNameResolver — and the bundle makes neither:
                                                         each seam picks its own arm and the bundle takes
                                                         what it is handed. Everything else is portable)
                                IAdapterInfoProvider.cs (seam + the AdapterSnapshot record)
                                AdapterInfoProvider.cs  (async snapshot: all adapters + primary IP config
                                                         via managed NetworkInterface; per-adapter/field
                                                         soft-fail. No platform prefix — portable; the one
                                                         Windows-only field (DHCP) is guarded inline → "—")
                                AdapterInfo.cs          (record + AdapterKind enum; fixed status-dot brushes)
                                IpConfigInfo.cs         (record: IPv4/mask/gateway/DNS/MAC/DHCP; .Unknown)
                                IConnectionsInterop.cs  (seam + ForCurrentPlatform() + the RawConnection
                                                         struct. ADDRESS FAMILIES ARE WHATEVER THE PLATFORM
                                                         CAN SUPPLY, not a fixed set — Linux includes IPv6,
                                                         Windows is IPv4-only. State is a MIB_TCP_STATE value
                                                         whatever the platform's own numbering is)
                                WindowsConnectionsInterop.cs
                                                        (feature-local iphlpapi P/Invoke:
                                                         GetExtendedTcpTable/GetExtendedUdpTable, IPv4
                                                         OWNER_PID tables; port byte-order swap. IPv6
                                                         deferred — the OWNER_PID tables use different
                                                         16-byte-address structs. Holds
                                                         UnsupportedConnectionsInterop — reports none)
                                LinuxConnectionsInterop.cs
                                                        (/proc/net/{tcp,tcp6,udp,udp6} over ProcNetParser,
                                                         owners via SocketInodeMap. TRANSLATES THE KERNEL'S
                                                         TCP STATE CODES TO THE MIB NUMBERING — the two
                                                         tables are unrelated (Linux LISTEN 0x0A is MIB
                                                         Last-ack, Linux ESTABLISHED 0x01 is MIB Closed), so
                                                         passing them through labels every row wrongly AND
                                                         plausibly. 8→8 is a coincidence, not a shared code.
                                                         Reports UDP as connectionless to match the Windows
                                                         row shape, even for a socket the kernel tracks as
                                                         connected. No [SupportedOSPlatform] — portable
                                                         managed reads over IProcFileSystem)
                                IProcessNameResolver.cs (seam + ForCurrentPlatform() + the shared Unnamed(pid)
                                                         wording. Naming a process is NOT portable even
                                                         though looking one up is)
                                WindowsProcessNameResolver.cs
                                                        (Process.GetProcessById + ".exe"; 0 → "System Idle",
                                                         4 → "System". Holds UnsupportedProcessNameResolver.
                                                         [SupportedOSPlatform] ON THE CTOR even though it
                                                         calls no Windows-only API: what it ENCODES is
                                                         Windows-only, and off Windows it would mislabel real
                                                         rows rather than fail. Verified load-bearing —
                                                         removing the factory guard fails the build)
                                LinuxProcessNameResolver.cs
                                                        (over the shared ProcPidName. No .exe and NO
                                                         WELL-KNOWN PIDS — PID 4 is an ordinary kernel thread
                                                         on Linux. A socket with no visible owner shows "—",
                                                         not PID 0, which is not a process)
                                IConnectionsProvider.cs (seam + the ConnectionsSnapshot record)
                                ConnectionsProvider.cs  (TCP+UDP snapshot off the UI thread; PID→name cache
                                                         with stale eviction; de-dupe by key; sort; cap 1000.
                                                         Takes BOTH seams by ctor. IPv6 endpoints are
                                                         BRACKETED — "::1:631" gives no way to tell the port
                                                         from another hextet, and the identity key is built
                                                         from these strings. SINGLE-CONSUMER: the name cache
                                                         is per-instance mutable state)
                                ConnectionInfo.cs       (record + composite identity Key)
                                ConnectionRow.cs        (mutable row VM: only State/StateBrush observable,
                                                         reused across polls via the keyed diff)
                                PingMonitor.cs          (reused in-box Ping; rolling avg/loss + last-3
                                                         lines; soft-fails to a timeout. NO DEFAULT TARGET:
                                                         Target starts EMPTY. It used to default to 8.8.8.8,
                                                         which is how the app came to ping a public resolver
                                                         from launch — and since SetTarget ignores a blank
                                                         value, leaving the constant would let an empty box
                                                         silently resolve back to it)
                                IDnsLookupProvider.cs   (seam + the DnsResult record)
                                DnsLookupProvider.cs    (one-shot Dns.GetHostEntryAsync with a 3 s CTS;
                                                         record type by address family. DefaultHost seeds
                                                         the BOX only — nothing resolves until the user
                                                         presses Look up. No platform prefix — portable)
```

## `src/Tabs/Hardware`

```
      /Hardware                 HardwareView.axaml(.cs) + HardwareViewModel.cs
                                                        (spec grid; whole-page scroll like the Dashboard
                                                         — not self-scrolling. VM builds the six fixed
                                                         HardwareCard models, populates them from
                                                         HardwareInfoProvider in the ctor, and implements
                                                         IRefreshablePage; Sensors card left as "—")
                                IHardwareInfoProvider.cs (seam + ForCurrentPlatform(); ONE interface over the
                                                         whole surface — the public shape is already a
                                                         single method returning one aggregate. Holds the
                                                         per-platform reader factories: Windows() carries
                                                         the SupportedOSPlatform because resolving the WMI
                                                         readers is the only Windows-specific step; Linux()
                                                         supplies processor + motherboard and leaves the
                                                         other three cards on Unsupported*)
                                HardwareInfoProvider.cs (async composer: one soft-failing section per card
                                                         → HardwareInfo, the five run concurrently. NO
                                                         platform prefix and NO attribute — the composition
                                                         and its per-card guard are portable, which is what
                                                         keeps them callable from tests on every platform.
                                                         Holds UnsupportedHardwareInfoProvider — .Unknown
                                                         for every card, for a platform with no readers)
                                HardwareInfo.cs         (aggregate snapshot record + per-card sub-records,
                                                         each with .Unknown; fields default to "—")
                                HardwareCard.cs         (observable: fixed title/icon/colours, observable
                                                         Subtitle + ObservableCollection<HardwareSpec> Rows)
                                HardwareSpec.cs         (observable: fixed Key, observable Value → "—")
                                HardwareIcons.cs        (feature-local card glyph geometries + fixed
                                                         per-card icon colours)
                                /Providers              one seam + reader per card, each with its own
                                                         never-throw fall back to that card's .Unknown, so
                                                         one dead source can't blank the others. WmiRead
                                                         holds the WMI boilerplate the Windows readers
                                                         share. Windows* for all five; Linux* for processor,
                                                         motherboard and storage; Unsupported* twins (at the
                                                         bottom of their Windows file) for memory modules
                                                         and graphics.
                                                         LinuxProcessorInfoProvider — the shared CpuFacts
                                                         plus its L3 read; SOCKET IS PERMANENTLY "—"
                                                         (SMBIOS type 4 needs dmidecode as root).
                                                         LinuxMotherboardInfoProvider — DmiIdReader +
                                                         HardwareCatalog, composing the same
                                                         "version (year)" BIOS string as the WMI arm; PCIE
                                                         SLOTS IS PERMANENTLY "—" (SMBIOS type 9 likewise;
                                                         /sys/bus/pci counts occupied devices, not slots).
                                                         LinuxStorageInfoProvider — the shared SysBlockFacts,
                                                         so this card and the Storage tab's cards cannot
                                                         disagree about a drive while keeping their own
                                                         wording ("NVMe" here, "NVMe SSD" there);
                                                         DRIVE HEALTH IS PERMANENTLY "—" (SMART needs root).
                                                         UnsupportedMemoryModulesProvider is permanent too —
                                                         per-DIMM facts are SMBIOS type 17.
                                                         ChipsetNames — the board-name token scan BOTH
                                                         platforms fall back to when the catalog has no
                                                         entry; ordered most-specific-first so B650E does
                                                         not match the B650 token)
                                /Catalog                HardwareCatalog.cs (facade + name normalizer +
                                                         longest-key match) over per-domain static spec
                                                         tables: CpuCatalog / GpuCatalog / BoardCatalog /
                                                         MemoryCatalog (each a spec record + Data dict).
                                                         Fills rated specs WMI can't report; unknown → "—")
```

## `src/Tabs/Performance`

```
      /Performance              PerformanceView.axaml(.cs) + PerformanceViewModel.cs
                                (LIVE — Task-Manager-style master-detail: a 220px resource-selector
                                 rail (ResourceRow item VMs) swaps a right detail pane — one large
                                 Sparkline utilization chart + a 4-tile stat strip (StatTile item VMs).
                                 Fills the viewport via ISelfScrollingPage, like File Explorer. All five
                                 resources (CPU/Memory/Disk/GPU/Ethernet) subscribe to the shared
                                 SystemMetricsService; IRefreshablePage/ILiveSamplingPage/IActivatablePage/
                                 IDisposable.)
                                CpuSpeedFormatter.cs    (Speed tile: the WMI base clock × the PDH clock
                                                         ratio, as GHz; "—" when either is missing)
                                MemoryCacheFormatter.cs (Cached tile: bytes → binary GB, "—" when the
                                                         provider reports nothing)
                                IGpuSensorProvider.cs   (GPU Temp/Power tiles. THE TWO PLATFORMS HAVE
                                WindowsGpuSensorProvider.cs OPPOSITE SHAPES. Windows has no in-box sensor
                                IGpuSensorReader.cs      API, so it fans out to one reader per vendor SDK:
                                GpuPciMatcher.cs         NVIDIA temperature via NVAPI (nvapi_QueryInterface
                                GpuSensorFormatter.cs    function-id dispatch, the DXGI vtable technique) and
                                NvApiInterop.cs          power via NVML; AMD temperature via ADL's PMLOG
                                NvmlInterop.cs           snapshot. Those SDKs report NO LUID, which is the
                                NvidiaGpuSensorReader.cs only reason GpuPciMatcher exists — adapters are
                                AdlInterop.cs            joined by PCI identity instead.
                                AmdGpuSensorReader.cs    LinuxGpuSensorProvider needs NEITHER: the kernel
                                PnpPciParser.cs          nests each card's hwmon under the card, so the
                                LinuxGpuSensorProvider.cs adapter key alone finds it and the pci argument is
                                                         unused. temp1_input is MILLIDEGREES and
                                                         power1_average MICROWATTS — two more scales, both
                                                         range-checked, since a wrong divisor still looks
                                                         plausible. No packages, no admin. Every vendor and
                                                         EVERY METRIC soft-fails to "—" independently.
                                                         The vendor readers' EMPTY ANNOTATED CONSTRUCTORS are
                                                         what make the three static interop classes
                                                         unreachable off Windows — see the CA1416 section.
                                                         AMD power + Intel are deferred — see Deferred work)
```

## `src/Tabs/Storage`

```
      /Storage                  StorageView.axaml(.cs) + StorageViewModel.cs
                                (LIVE — read-only drives/health view: a top row of DriveCard summary
                                 cards over a Partitions table (PartitionRow item VMs) + a Disk Activity
                                 card (shared Sparkline, ChartStorage key). Page-scrolls like Network
                                 (not ISelfScrollingPage). Cards from PhysicalDiskProvider/StorageComposer/
                                 VolumeProvider; Disk Activity + Queue from the page-local throughput sampler
                                 feed; per-disk Read/Write from IPhysicalDiskThroughputSampler; NVMe Temp
                                 from DiskTemperatureProvider (IOCTL health log). IRefreshablePage/
                                 ILiveSamplingPage/IActivatablePage/IDisposable.)
```

## `src/Tabs/Processes`

```
      /Processes                (the tab itself is described under Feature notes; only its seams and the
                                 column model are mapped here)
                                ProcessColumnId.cs      (enum: the seven columns, declaration order =
                                                         the order the table ships in)
                                ProcessColumns.cs       (one table of per-column minimum width + weight,
                                                         and the pinned column. Indexed BY THE ENUM, so
                                                         the two must stay in the same order — asserted)
                                ProcessColumnOrder.cs   (codec + Resolve for the user's column order.
                                                         Encoding is EnumListCodec's; only Resolve, which
                                                         forces the pinned column leftmost, is local)
                                ProcessSortState.cs     (codec for the remembered sort column+direction)
                                IProcessTerminator.cs   (seam: ends a process. Exists so End task is
                                                         testable — see ProcessTerminator.cs beside it)
                                IProcessInterop.cs      (seam + ForCurrentPlatform())
                                WindowsProcessInterop.cs
                                                        (kernel32 I/O counters + shell32 Properties sheet.
                                                         Holds UnsupportedProcessInterop. Duplicated from
                                                         File Explorer's shell interop ON PURPOSE — the
                                                         self-contained-tab rule)
                                LinuxProcessInterop.cs  (resolves /proc/[pid]/exe and reveals its folder,
                                                         since no desktop offers a Properties dialog for
                                                         a foreign process. TryGetIoBytes reports nothing
                                                         and that costs the tab NOTHING: the Linux
                                                         snapshot provider does not take this seam and
                                                         reads /proc/[pid]/io itself)
```
