# DashDetective

[![.NET Desktop (Avalonia)](https://github.com/SophiaScherer/DashDetective/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/SophiaScherer/DashDetective/actions/workflows/dotnet-desktop.yml)

A system-information console built with [Avalonia UI](https://avaloniaui.net/) and .NET 10.
It presents live machine metrics and hardware details across eight tabs — Dashboard, File Explorer,
Processes, Performance, Network, Storage, Hardware and Settings — in a single themeable window.

Most of it can be driven from the keyboard: `Ctrl+1`–`Ctrl+8` jump between tabs, `F5` refreshes,
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

The build sets `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild`, so any compiler warning or
`.editorconfig` style violation fails the build. Verify formatting the way CI does before pushing:

```powershell
dotnet format DashDetective.sln --verify-no-changes
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

The [.NET Desktop (Avalonia)](.github/workflows/dotnet-desktop.yml) workflow runs on
`windows-latest` across a `Debug` and `Release` matrix. Each leg restores, verifies formatting with
`dotnet format --verify-no-changes`, builds, and runs the test suite with Cobertura coverage. The
coverage report is uploaded as a per-configuration artifact, and the `Release` leg also publishes
the compiled application as a downloadable artifact.

## Project Structure

```
DashDetective/
  Program.cs, App.axaml            application bootstrap
  app.manifest                     Win32 application manifest
  Assets/                          application icon
  src/
    Shared/                        ViewModelBase, page marker interfaces, AppInfo,
                                   Charts, Controls, Shortcuts, Styles, formatters
    Services/                      Theming, SystemMetrics, Network, Settings, Startup,
                                   Diagnostics, Identity, Threading
    Shell/                         MainWindow, MainWindowViewModel, ViewLocator,
                                   Navigation, Help, Shortcuts
    Tabs/                          Dashboard, FileExplorer, Processes, Performance,
                                   Network, Storage, Hardware, Settings
tests/
  DashDetective.Tests/             xUnit test suite
docs/
  ARCHITECTURE.md                  architecture reference
.github/workflows/                 CI
```

Source layout and design conventions are documented in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

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
