# Administrator label in the nav footer

Work item 70, Sprint 4, *Standard user label is not correct.*

## Root cause

`CurrentUserProvider` decided the role with `WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)`.
With UAC on, which is the Windows default, an administrator's apps run with a **filtered token**. In that
token the Administrators group is kept but marked *deny-only*, and a role check counts only enabled groups.
So unless DashDetective was started with "Run as administrator", the check failed and the footer said
"Standard User" for every administrator. Both the project owner and the tester saw exactly that.

## What was built

- **Windows:** a token whose role check passes is *elevated*. Otherwise the provider looks for the
  Administrators SID (`S-1-5-32-544`) among the token's `DenyOnlySid` claims. .NET's `WindowsIdentity`
  emits one claim per deny-only group (`WindowsIdentity.cs`, `AddGroupSidClaims`). If the SID is there,
  the account is an administrator running unelevated. Neither case needs P/Invoke.
- **Linux (newly defined):** root is elevated. A member of `sudo`, `wheel` or `admin` is an administrator.
  Anyone else is a standard user. Membership comes from the process's own `Groups` line in
  `/proc/self/status`, matched by gid against `/etc/group`. `/etc/group`'s member lists aren't used,
  because they omit each user's primary group.
- **Labels:** elevated and member both read **"Administrator"**, because the footer describes the account.
  A non-admin reads "Standard User". Anything unreadable reads the neutral "User", as before. macOS
  keeps "User".

## Decisions

- **One label for elevated and unelevated administrators.** The AC asks the footer to "report administrator
  when the account is elevated or in the admin group". The token's elevation state is still read and kept
  apart (`AdminStatus.Elevated` vs `Member`), so a later change could show it, for example in the tooltip.
  I didn't add that here, to keep the footer's layout untouched.
- **A deny-only claim rather than `GetTokenInformation(TokenElevationType)`.** It is managed, needs no new
  P/Invoke surface for CA1416 to miss, and the claim is exactly the filtered Administrators group.
- **Unknown stays unknown.** A missing status file, a missing `Groups` line or an unreadable
  `/etc/group` gives "User", never "Standard User".

## How to verify

1. On Windows, signed in as an administrator, start DashDetective normally, without elevation. The
   footer's role line reads **Administrator**. It used to read "Standard User".
2. Start it with "Run as administrator": still **Administrator**.
3. Sign in as a standard (non-admin) account: **Standard User**.
4. On Linux, as a user in `sudo` (Ubuntu) or `wheel` (Fedora): **Administrator**. As a user in neither:
   **Standard User**. Under `sudo`: **Administrator**.
