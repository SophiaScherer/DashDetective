# Saved shortcut bindings are read by name only

Work item 126.

## What was wrong

`ShortcutOverrideCodec.Decode` used `Enum.TryParse`, which accepts a number whether or not the enum defines
it. A saved "3" loaded as the action at ordinal 3 and "999" as an id that does not exist, which is the
re-pointing by ordinal the codec's name-only rule exists to prevent.

## What was built

- Ids, keys and modifiers go through a name-only parse: every comma-separated part must be a member name.
  The entry is skipped otherwise, and the rest of the file still loads.

## Decisions

- **The check is on the name, not on `Enum.IsDefined` alone.** `IsDefined` accepts numbers for the underlying
  type and cannot judge a flags combination such as "Control, Shift"; matching each part against the member
  names handles both.
- **`Encode` is unchanged,** and its output still round-trips.

## How to verify

1. Edit `settings.json` so a shortcut entry's id, key or modifier field is a number.
2. Start the app. That shortcut keeps its default and the other rebinds load.
