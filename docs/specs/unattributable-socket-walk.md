# Skip the fd walk for sockets that cannot be attributed

Work item 101: *Linux connections poll re-walks every process's fds when a socket cannot be attributed.*

## What was wrong

On Linux the Network tab's Active Connections table names a socket's owner by walking every process's
`/proc/[pid]/fd` for `socket:[inode]`. `SocketInodeMap` caches that walk and only repeats it when asked
about an inode it has not mapped. Another user's socket, such as systemd-resolved on 127.0.0.53:53 or
cupsd on :631, can never be mapped, because that user's fd directory is unlistable. Each 2.5 s poll
therefore walked every process twice, once for TCP and once for UDP, for as long as such a socket was
open, which on a desktop install is always.

## What was built

- `SocketInodeMap` remembers inodes a walk could not find as **unattributable**, and a poll asking only
  about mapped or unattributable inodes does not walk.
- A genuinely new inode still forces a walk, and that walk re-tests every unattributable inode.

## Decisions

- **The state lives in `SocketInodeMap`, not in `IProcFileSystem`.** The filesystem seam must stay
  stateless, and the map already owned the cache.
- **The set is rebuilt from the inodes asked about since the previous walk's call.** The connections
  reader calls the map twice per poll, TCP then UDP, and a walk either one triggers must keep the
  other's unattributable inodes. Otherwise every new TCP socket would cost a second walk on the UDP call.
  Rebuilding from what was asked also lets closed sockets drop out instead of piling up.
- **A socket that becomes visible later waits for the next walk.** One that was unattributable and is
  then passed to a process the user can see is attributed only when some new socket forces a walk. That
  is rare, and the earlier behavior paid two full walks per poll to cover it.

## How to verify

1. On a Linux desktop, open the Network tab and let Active Connections poll for a few cycles.
2. Run `strace -f -e trace=readlink -p <pid>` against the app (or watch its CPU in `top`). Steady-state
   polls should issue no `readlink` calls under `/proc/*/fd`, even with systemd-resolved listening.
3. Open a new connection (for example `curl https://example.com`). The next poll walks once, and the new
   row names `curl`.
4. Rows for root-owned listeners still show "—" as their process, as before.
