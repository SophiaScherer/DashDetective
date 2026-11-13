# Discard a stale free-space read

Work item 110.

## What was wrong

`ResourceAlertWatcher.SampleSpaceAsync` read its threshold, awaited the (slow, WMI) volume list, and then
wrote the verdict without checking anything again. Switching the low-space bell on and then off before the
read finished cleared the state, and the stale continuation set it again. The 60 s timer had already been
stopped, so the banner stayed up, naming a disk whose alert was off. Overlapping reads could also finish
out of order.

## What was built

- Each read takes a generation number. After the await it is discarded unless it is still the newest and
  the master switch is on, the Live pill is on and the threshold is the one it started with.

## Decisions

- **No immediate read on the master switch.** The docs promise one only when the bell is switched on, and
  that stays as is.
- **A discarded read changes nothing**, including the previous verdict, the same as a failed enumeration.

## How to verify

1. Run `ResourceAlertWatcherTests`: the three `LowSpaceRead_*` tests hold a read open with a
   `TaskCompletionSource`, change the setting, then complete the read.
