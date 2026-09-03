# DashDetective

[![.NET Desktop (Avalonia)](https://github.com/SophiaScherer/DashDetective/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/SophiaScherer/DashDetective/actions/workflows/dotnet-desktop.yml)

A system-information console built with [Avalonia UI](https://avaloniaui.net/) and .NET 10.
It presents live machine metrics and hardware details across nine tabs — Dashboard, File Explorer,
Processes, Performance, Network, Storage, Hardware, Toolkit and Settings — in a single themeable
window.

Most of it can be driven from the keyboard: `Ctrl+1`–`Ctrl+9` jump between tabs, `F5` refreshes,
`Ctrl+P` pauses live sampling and `Ctrl+Shift+T` flips the theme. Press `F1` for the full list.

## Requirements

1. [.NET 10 SDK](https://dotnet.microsoft.com/download) — both projects target `net10.0`
2. Windows 10 or 11 for the full feature set (see [Platform Support](#platform-support))
3. Git

## Building and Running

1. Clone the repository:

   ```powershell
   git clone https://github.com/SophiaScherer/DashDetective.git
   cd DashDetective
   ```

2. Restore dependencies:

   ```powershell
   dotnet restore DashDetective.sln
   ```

3. Build the solution:

   ```powershell
   dotnet build DashDetective.sln -c Release
   ```

4. Run the application:

   ```powershell
   dotnet run --project DashDetective
   ```

`Directory.Build.props` sets `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild` and
`AnalysisLevel=latest` for both projects, so any compiler warning, analyzer diagnostic or
`.editorconfig` style violation fails the build. Verify formatting the way CI does before pushing:

```powershell
dotnet format DashDetective.sln --verify-no-changes
```

If the app is running — or an IDE is holding `bin/` — a build fails with `MSB3027`. Build into a
separate tree to avoid it:

```powershell
dotnet build DashDetective.sln -c Release --artifacts-path artifacts
```

To produce a self-contained output directory, use the same command CI publishes with:

```powershell
dotnet publish DashDetective/DashDetective.csproj -c Release -o publish
```

## Testing

Unit tests live in `tests/DashDetective.Tests` and use xUnit with hand-rolled fakes — no mocking
framework. Coverage is collected through coverlet.

```powershell
dotnet test DashDetective.sln
```

```powershell
dotnet test DashDetective.sln --collect:"XPlat Code Coverage"
```

## Continuous Integration

The [.NET Desktop (Avalonia)](.github/workflows/dotnet-desktop.yml) workflow runs four legs —
`windows-latest` and `ubuntu-latest`, each in `Debug` and `Release` — so a change that breaks one
platform fails the run. Every leg restores, verifies formatting with
`dotnet format --verify-no-changes`, builds, and runs the test suite with Cobertura coverage. The
coverage report is uploaded per OS and configuration, and the Windows `Release` leg also publishes
the compiled application as a downloadable artifact.

Two further legs build and test on `macos-latest`, **reporting rather than gating** — the job carries
`continue-on-error`. They claim nothing about macOS *support*: every `ForCurrentPlatform()` seam
resolves to its `Unsupported*` arm there, so most readings would show "—".

[CodeQL](.github/workflows/codeql.yml) scans the C# sources on every push and pull request, and
weekly on a schedule. It covers what the in-build analyzers cannot: `CA1416` only sees annotated
BCL APIs, so the hand-written `DllImport` declarations are invisible to it. Findings appear under
Security → Code scanning; they report rather than fail the build.

Dependabot proposes NuGet and GitHub Actions updates weekly, grouped into one pull request per
ecosystem.

## Project Structure

```
Directory.Build.props              shared TFM and analyzer gates
DashDetective/
  Program.cs, App.axaml            application bootstrap
  app.manifest                     Win32 application manifest
  Assets/                          application icon
  src/
    Shared/                        ViewModelBase, page marker interfaces, formatters,
                                   Charts, Controls, Layout, Shortcuts, Styles, Completion
    Services/                      SystemMetrics, Platform, Theming, Settings, Network,
                                   Search, Startup, Diagnostics, Identity, Threading
    Shell/                         MainWindow, MainWindowViewModel, ViewLocator,
                                   Navigation, Search, Help, Shortcuts, TrayNotice
    Tabs/                          Dashboard, FileExplorer, Processes, Performance,
                                   Network, Storage, Hardware, Toolkit, Settings
tests/
  DashDetective.Tests/             xUnit test suite, mirroring src/ path for path
docs/
  ARCHITECTURE.md                  how the layers and seams fit together
  SOURCE-MAP.md                    every file under src/, and the trap it avoids
  FEATURES.md                      what each shipped feature does
.github/workflows/                 CI and CodeQL
```

The layers and seams are documented in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), the individual
files in [docs/SOURCE-MAP.md](docs/SOURCE-MAP.md), and each feature in
[docs/FEATURES.md](docs/FEATURES.md). Conventions for changing the code are in
[AGENTS.md](AGENTS.md).

## Platform Support

**Windows is the fully supported platform.** The application reads the machine through facilities that
exist only on Windows: WMI (`System.Management`) for static hardware identity, PDH performance counters
for live CPU/GPU/disk metrics, the registry for build details, and Win32 P/Invoke (`shell32`,
`iphlpapi`, `IOCTL_STORAGE_QUERY_PROPERTY`) for shell integration, the connections table and NVMe drive
temperature. No elevation is required — the application runs as a standard user.

**Linux support is in progress.** Both projects now target a neutral `net10.0` TFM and CI builds on
`ubuntu-latest`, so the app compiles and launches on Linux. Data sources are being ported one at a time;
until each lands, the affected panel reads "—" rather than failing. Every reader degrades on its own, so
a missing source is never a crash. macOS is not started.
