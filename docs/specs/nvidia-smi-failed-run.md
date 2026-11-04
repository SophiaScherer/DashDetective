# Clear NVIDIA utilization when nvidia-smi starts failing

Work item 112.

## What was wrong

`NvidiaSmiReader` replaced its readings only when a run exited 0 without timing out. After a good run, a
later failure (a driver and library version mismatch after an update, or a timeout) left the last good
readings in place, so the Linux NVIDIA utilization tile froze at its last value and looked live. The
class comment promises readings are replaced wholesale rather than freezing.

## What was built

- Every completed run replaces the readings: parsed output on success, empty on a non-zero exit or timeout.
- An internal constructor parameter sets the refresh window, so a test needs no 15 s wait.

## Decisions

- **A thrown launch still retires the reader**, as before. Only a returned failure clears the readings.
- **The 15 s cadence is unchanged** in production.

## How to verify

1. Run `NvidiaSmiReaderTests`: `RefreshIfDue_AFailedRunAfterASuccessfulOne_ClearsTheReadings` stages a good
   run, then an exit-9 run and a timeout, and expects the utilization to go back to null.
