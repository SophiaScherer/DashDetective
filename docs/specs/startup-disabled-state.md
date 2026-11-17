# Launch at startup honors a disabled entry

Work item 111.

## What was wrong

On Windows, `IsEnabled` only checked that the HKCU `Run` value existed. Task Manager's Startup apps tab
disables an app by writing `Explorer\StartupApproved\Run` and leaves the `Run` value alone, so the Settings
toggle read On for an app that would not start, and switching it on again did not clear the flag. On Linux,
GNOME's startup settings write `X-GNOME-Autostart-enabled=false` into the `.desktop` file, which
`DesktopEntry.IsEnabled` ignored.

## What was built

- `StartupApproval.IsDisabled` decodes the `StartupApproved` value: the first byte's low bit set (0x03,
  0x07) means disabled; 0x02, 0x06 or no value means enabled.
- `WindowsStartupRegistration` reads both values through an `IStartupRunStore` seam. Enabling deletes a
  disabled flag; disabling removes only the `Run` value.
- `DesktopEntry.IsEnabled` treats `X-GNOME-Autostart-enabled=false` as disabled. Enabling on Linux already
  rewrites the file with `=true`.

## Decisions

- **The flag is deleted, not rewritten to 0x02.** A missing value reads as enabled, and Windows rewrites it
  the next time the user touches Task Manager.
- **Only the parameterless constructor carries `[SupportedOSPlatform("windows")]`**, since it is what
  builds the registry store. The interface is untouched, and the logic runs on every CI leg.
- **Other first-byte values** (anything odd) read as disabled; only 0x02/0x06 and 0x03/0x07 are known.

## How to verify

1. Run `StartupApprovalTests`, `WindowsStartupRegistrationTests` and `StartupRegistrationTests`.
2. On Windows: turn Launch at startup on, disable DashDetective in Task Manager, reopen Settings: the
   toggle is off. Switch it on: Task Manager shows Enabled.
