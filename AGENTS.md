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

**Everything else (File Explorer, Processes, Performance, Network, Storage, Hardware) is
out of scope until this document says otherwise.** Do not scaffold, stub, reference, or
"prepare" folders for inactive features, even if it seems convenient or efficient. Wait until
they are explicitly activated in a future revision of this file.

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

There is a design document describing UI/UX intent, layout, and behavior for
each feature. You may read and update this document as part of feature work on the current
feature(s). Location: `/docs/DESIGN/`.

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
        Sparkline, StatCard, InfoRow   (reusable widgets)
    /Shell                      (the app frame — the "default window")
      MainWindow.axaml(.cs), MainWindowViewModel.cs, ViewLocator.cs
      /Navigation
        NavItem.cs, Icons.cs
    /Tabs                       (one self-contained folder per feature)
      /Dashboard                DashboardView.axaml(.cs) + DashboardViewModel.cs
      /Settings                 SettingsView.axaml(.cs) + SettingsViewModel.cs
      (FileExplorer, Processes, Performance, Network, Storage, Hardware — not yet started)
```

Namespaces follow folders: `DashDetective.Shared`, `DashDetective.Shared.Controls`,
`DashDetective.Shell`, `DashDetective.Shell.Navigation`, `DashDetective.Tabs.<Feature>`.
The `ViewLocator` maps a `*ViewModel` to its `*View` by name, so a tab's View and ViewModel
must share a namespace.

Rules of thumb:
- Anything reused by more than one tab (styling, colours, widgets) belongs in `src/Shared`.
- Keep each tab self-contained: its view, view model, and feature-specific helpers live in
  its own folder under `src/Tabs`, not scattered project-wide.
- The shell (sidebar/toolbar/navigation) is shared — edit carefully.

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