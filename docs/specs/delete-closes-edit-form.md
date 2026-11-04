# Deleting a command closes its edit form

Work item 122: *Saving a Utilities edit after deleting that command silently discards it.*

## What was wrong

A user command's row has an Edit and a Delete button. With the edit form open on a command, deleting that
command left the form open. Save then called `UpdateCommand`, which finds the row by reference, found
none, and returned without a word, so the edit was dropped and the form closed as if it had worked.

## What was built

- `ToolkitCommandFormViewModel.CancelIfEditing(command)` closes the form when it is editing that command.
- `ToolkitViewModel.RemoveCommand` calls it after a successful removal.

## Decisions

- **Close, not confirm.** The edited text belongs to a command that no longer exists, so closing is the
  honest outcome. A delete confirmation is a separate story and is not added here.
- **Only the matching command.** Deleting some other row leaves an open form, and any text typed in it,
  untouched. A form that is adding a new command is never affected.
- **The check is by reference** to the command the form was opened on, the same identity `UpdateCommand`
  uses.

## How to verify

1. Open Utilities, add a command, then press its Edit button so the form opens.
2. Press the command's Delete button. The form closes.
3. With a second command, edit the first and delete the second. The form stays open.
