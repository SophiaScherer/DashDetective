# One unusable settings value no longer resets every setting

Work item 129, Sprint 10: *One unknown enum name in settings.json resets every setting.*

## What was wrong

`SettingsStore.Load` merged the file over the defaults and deserialized the whole object. A single value
that did not fit its property, such as an enum name written by another version, threw, and `Load` answered
with `AppSettings.Defaults` for everything. The next save then overwrote the file, losing every setting.

## What was built

- When the whole-object deserialize throws a `JsonException`, `Merge` probes each file key on its own over
  the defaults and resets only the keys that fail to their defaults, then deserializes again.

## Decisions

- **Whole-file corruption still loads defaults.** Unparseable JSON, a non-object root, or a schema version
  mismatch behave as documented.
- **The happy path is unchanged.** The per-key probe runs only after the whole load has failed.

## How to verify

1. Edit `settings.json` so `"ColorVision"` is `"NoSuchMode"` and `"ShowInTray"` is `false`.
2. Start the app: the tray icon is off and color vision is at its default; the other settings are kept.
