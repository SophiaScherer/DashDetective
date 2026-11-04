# Toolkit codec accepts names only

Work item 144, Sprint 10 exam: *the saved-commands codec accepts numeric command types.*

## What was wrong

`ToolkitCommandCodec` parsed the type and category fields with `Enum.TryParse`, which also accepts a
number. A hand-edited or corrupted `"1"` loaded as whichever member held that value, and `"99"` loaded as
an undefined member, though the codec only ever writes names.

## What was built

- Both fields go through one `TryParseName` that requires an exact member name before parsing.

## Decisions

- **An unknown or numeric type drops the record.** The payload means something different per type, so
  guessing could run the wrong thing.
- **An unknown or numeric category keeps the command unfiled**, as an unknown name already did.
- **Codecs store names, never ordinals**, per the Settings rule; this closes the read side of it.

## How to verify

1. Hand-edit `settings.json` so a custom command's type reads `1`.
2. Restart: that command is gone and the others remain.
