# Quote the Toolkit command line

Work item 145, Sprint 10 exam: *the displayed command line joins target and arguments unquoted.*

## What was wrong

`ToolkitAction.CommandLine`, shown on the Execution Log's "$" line and copied by Copy command, joined the
target and arguments with spaces. A path such as `C:\Program Files\x.exe` or an argument such as `a b`
split into several words when pasted into a terminal.

## What was built

- A token containing whitespace or a quote is quoted by the CommandLineToArgvW rules (the ones .NET's
  ArgumentList follows): embedded quotes are escaped and backslashes before a quote or the closing quote
  are doubled.
- The target is shown environment-expanded through `ToolkitPaths.Resolve`, as the runner runs it. A
  documentation URL is left as written.

## Decisions

- **Display only.** The runner still receives the target and the unjoined argument list, so what actually
  runs does not change.
- **Tokens without whitespace or quotes are untouched**, so existing lines such as `ping -c 4 8.8.8.8` read as before.

## How to verify

1. Run a Toolkit row whose target lives under `Program Files`.
2. The "$" line shows the path in quotes; pasting it into a terminal runs the same command.
