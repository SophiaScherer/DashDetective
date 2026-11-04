# A ping that finishes after Stop stays silent

Work item 141.

## What was wrong

Pressing Stop while a send was still in flight (an unreachable host takes the 1.5 s timeout) set the summary
to "Press Start to ping". When the send then completed, `RunPingAsync` wrote the console and summary
without checking whether the monitor was still on, so the stats came back under Start.

## What was built

- `NetworkViewModel` keeps a ping session number, bumped by every Start and Stop. `RunPingAsync` captures
  it before the send and publishes only if it is still current afterward.

## Decisions

- **A restart also ends the old session,** so a reply from before the restart cannot fill the new session's
  fresh console. The first line of the new session then arrives with the next tick or Refresh.
- **The monitor's own stats are untouched.** Only publishing to the panel is gated.
- **No ping seam was added.** The test uses the real monitor against TEST-NET-1, which never answers.

## How to verify

1. Start a ping at an unreachable host (192.0.2.1) and press Stop within a second.
2. Wait past the timeout. The summary still reads "Press Start to ping" and the console stays empty.
