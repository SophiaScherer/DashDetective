# Refresh repeats the last submitted DNS lookup

Work item 142.

## What was wrong

After one lookup, the toolbar Refresh on the Network tab resolved whatever was in the DNS box at that
moment, including text typed but never submitted. An emptied box resolved "(none)" and read "Could not
resolve (none)". The code comment promised Refresh would repeat the lookup the user asked for.

## What was built

- `NetworkViewModel` remembers the host of the last submitted lookup. `LoadDnsAsync` takes the host as a
  parameter, and Refresh passes the remembered one. Before any lookup there is none, so Refresh still
  resolves nothing.

## Decisions

- **Only a submit updates the remembered host.** Typing in the box does not.
- **An existing test changed.** `Refresh_SupersedingAnInFlightLookup_DropsTheOlderAnswer` had asserted the
  old behavior (Refresh resolving an edited box); it now checks that the refresh's lookup supersedes the
  one still in flight.

## How to verify

1. Look up one host, then type a different host into the box without submitting.
2. Press Refresh. The panel shows the first host again, not the typed one.
3. Clear the box and press Refresh: the first host is still resolved.
