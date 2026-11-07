# Launch at startup toggle reflects the real state

Work item 146, Sprint 10 exam: *the toggle stays on after the registration write fails.*

## What was wrong

`OnLaunchAtStartupChanged` called `IStartupRegistration.SetEnabled` and never looked at the result. When the
write failed (registry denied, unsupported platform), the toggle stayed on and `true` was saved, so the
page and the settings file claimed something the OS did not do.

## What was built

- After `SetEnabled`, the view model re-reads `IsEnabled()`. If it differs from the requested value, the
  toggle is set back to the real state before `Changed` fires, so the saved value is the real one too.

## Decisions

- **The revert assigns the backing field and raises `PropertyChanged`.** Assigning the property would re-enter
  the hook and issue a second, pointless write.
- **No message is shown.** The toggle snapping back is the signal; a banner needs a product decision.

## How to verify

1. On a platform where startup registration is unsupported, flip Launch at startup on.
2. The toggle returns to off, and `settings.json` keeps `launchAtStartup` false.
