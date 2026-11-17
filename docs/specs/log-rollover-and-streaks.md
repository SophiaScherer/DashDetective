# Daily log rollover and one log line per failure streak

Work item 128, Sprint 10: *Log never rolls over and spams per tick.*

## What was wrong

`Log` documented a per-day rolling file but resolved the file name once, when the type loaded, so a
session running past midnight kept appending to the day it started. And `ResourceAlertWatcher` logged a
warning on every tick while its GPU or disk sampler kept throwing, which grew the file without bound.

## What was built

- `Log` keeps the log directory and builds the file name from the day of each write (`Log.FilePath`).
- `FailureStreak` reports the first failure of an unbroken run. The watcher's GPU and disk sampling logs
  only that one and re-arms after a successful sample.

## Decisions

- **No size cap or retention.** Only the rollover and the per-tick repeat were in scope.
- **Only the two per-tick watcher sites use a streak.** The volume and device-name reads are not repeated
  every tick.

## How to verify

1. Run `LogTests` and `FailureStreakTests`.
2. Make a sampler throw continuously: the log gains one watcher line, not one per tick.
