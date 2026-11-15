# Name processes that rewrite their title

Work item 103: *Linux process names garbled for programs that rewrite their command line.*

## What was wrong

On Linux the Processes tab and the Network tab's connection owners name a process from
`/proc/[pid]/cmdline`: argv[0] up to the first NUL, then the part after its last `/`. Programs that set
their own title (sshd, Chromium, postgres, nginx) overwrite the argument area with one space-joined string
padded with NULs. The whole title became argv[0], and its last `/` segment was junk. An SSH session's
`sshd: sophia@pts/0` was listed as "0", and a Chromium renderer was named after one of its flag values.

## What was built

- `ProcPidName` recognizes a rewritten title and names the process by its `comm` instead, so those rows
  read "sshd", "chrome" and "postgres".
- Ordinary command lines are unchanged, including a path that contains a space.

## Decisions

- **A title is judged by its shape, not by a space alone.** argv[0] counts as a title only when it
  contains a space, no argument follows it after a NUL, and its basename is not `comm`. A path such as
  `/opt/My App/run` has a space too, and when it carries arguments they sit after a NUL. When its basename
  matches `comm`, it is kept as written.
- **A 15-character `comm` only has to be a prefix.** The kernel truncates `comm` at 15 characters, so
  `systemd-resolve` still confirms `/opt/Some Vendor/systemd-resolved`.
- **cmdline stays the preferred source.** `comm` is the fallback only for a title, because the reason
  cmdline was chosen still holds: `comm` truncates long names.
- **No `comm` means no judgment.** With nothing to check against or fall back on, argv[0] is taken as
  written, as before.

## How to verify

1. On a Linux desktop, SSH into the machine from another host, then open the Processes tab.
2. The session's process reads "sshd" (or "sshd-session" on newer OpenSSH), not "0".
3. Open Chromium or Chrome. Its renderer processes read "chrome", not a flag value.
4. A program installed under a path with a space (for example `/opt/My App/run`) is still listed by its
   own name.
5. Where the Network tab can see the owner of the SSH connection, it shows the same name as the Processes tab.
