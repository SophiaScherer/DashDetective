# Dashboard CPU caption culture

Work item 137, Sprint 10 exam: *the CPU clock is formatted in the current culture.*

## What was wrong

`DashboardViewModel.FormatCpuModel` interpolated the base clock with `{…:F2}`, which uses the current
culture. On a comma-decimal system the caption read `4,70GHz` while the rest of the app prints a decimal
point.

## What was built

- The string is built with `CultureInfo.InvariantCulture`, matching the Hardware tab's spec formatters.

## Decisions

- **`FormatCpuModel` is `internal`** so a test can call it under a `de-DE` culture without a provider.

## How to verify

1. Set Windows' regional format to German and open the Dashboard.
2. The System Information CPU row reads `@ 4.70GHz`, not `@ 4,70GHz`.
