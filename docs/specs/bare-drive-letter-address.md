# Bare drive letter in the address bar

Work item 114: *Typing "C:" in the File Explorer address bar opens the wrong folder.*

## What was wrong

`CommitPath` passed the typed text to `Path.GetFullPath`. A bare drive letter is **drive-relative** on
Windows, so `"C:"` resolved to the process working directory on that drive (the install folder, or
`System32` when launched from the startup Run key) instead of the drive root. Relative names such as
`Users` resolved against the same directory.

## What was built

- `FileExplorerViewModel.RootTypedPath` runs before the folder check. A bare drive letter becomes that
  drive's root (`C:` to `C:\`). Anything still relative is refused with the existing
  `Can't find "<text>"` error and the box stays open.

## Decisions

- **`C:foo` is refused.** It is also drive-relative, and rooted according to `Path.IsPathRooted`, so it
  needs its own check.
- **The drive-letter rule is Windows-only.** On Linux `C:` is an ordinary relative name and is refused.
- **No new message.** The existing "Can't find" error already tells the user the text did not name a
  folder.

## How to verify

1. Open File Explorer, press Ctrl+L, type `C:` and press Enter. The `C:\` root opens.
2. Type `Users` and press Enter. The box stays open with "Can't find "Users"".
3. Type a full path such as `C:\Users`. It opens as before.
