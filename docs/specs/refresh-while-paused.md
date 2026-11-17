# Refresh while paused

Work item 104: *Refresh while paused does not update CPU, memory or network.*

## What was wrong

Turning the Live pill off pauses the shared metrics service, and each page drops its subscriptions so
nothing keeps a feed open while paused. The toolbar Refresh then called `RefreshAll`, which sampled only
feeds that had subscribers. The paused page had none, so the Dashboard, Performance and Processes tabs
kept their CPU, memory and network figures frozen. Only the page-local disk and GPU readouts moved.

## What was built

- `RefreshAll` samples every feed, subscribed or not, and renews each feed's cached latest sample.
- `MetricSubscriptions.Replay()` attaches and releases at once. A paused page takes the fresh sample
  through it and holds no subscription afterward.
- Each page's Refresh calls `Replay()` after `RefreshAll()`.
- A feed whose last read failed replays that failure to a new subscriber.

## Decisions

- **The page still drops its subscriptions while paused.** The page-lifecycle rule stays as it is.
  Replay borrows the attach that already seeds a page returning to screen. The service is paused, so that
  attach starts no timer.
- **The cache is renewed before the replay**, so the page gets one fresh sample. Attaching first would
  replay the stale cached value and then the fresh one, adding a duplicate point to every chart.
- **A failure is replayed as a failure.** Otherwise a paused page whose refresh failed would be handed
  the last good value as if it were current, which is the confident-wrong-answer case the soft-fail rules
  forbid.

## How to verify

1. Open the Dashboard and turn the Live pill off. The charts stop.
2. Start some CPU load, then press Refresh. CPU, memory and network each update once, and the charts
   advance by one point.
3. Wait a few seconds. Nothing moves until the next Refresh.
4. Repeat on Performance (CPU, Memory and network rows) and Processes (the CPU and Memory summary).
5. Turn Live back on. Sampling resumes as before.
