using DashDetective.Tabs.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Covers what is particular to <see cref="LinuxToolkitCatalog"/>'s table. The rules it shares with
/// every other catalog live in <see cref="ToolkitCatalogTests"/>'s counterpart set.
///
/// The catalog is named rather than resolved through
/// <see cref="IToolkitCatalog.ForCurrentPlatform"/>, so every assertion here runs on a Windows dev
/// machine too — the table is string literals, and none of it needs a Linux host to be true.
/// </summary>
public class LinuxToolkitCatalogTests {
    private static IReadOnlyList<ToolkitEntry> Entries => LinuxToolkitCatalog.Instance.Entries;

    /// <summary>Elevation means the Windows <c>runas</c> verb; there is no Linux equivalent wired up,
    /// so a row asking for it here would be a button that silently does the unelevated thing.</summary>
    [Fact]
    public void Entries_NoRowAsksForElevation() {
        Assert.DoesNotContain(Entries, e => e.RequiresElevation);
    }

    /// <summary>Ubuntu and Debian set <c>kernel.dmesg_restrict</c>, so a non-root dmesg only ever
    /// prints a permission error. Pinned by name because it reads as an obvious row to add back.</summary>
    [Fact]
    public void Entries_DoNotIncludeDmesg() {
        Assert.DoesNotContain(Entries, e => e.Action.Target == "dmesg");
        Assert.Contains(Entries, e => e.Command == "journalctl -k -n 50");
    }

    /// <summary>Linux ping runs until it is interrupted, so a row without a count would end at the
    /// timeout on every single run rather than reporting.</summary>
    [Fact]
    public void Ping_CapsTheNumberOfRequestsSoItTerminates() {
        var ping = Assert.Single(Entries, e => e.Action.Target == "ping");

        Assert.Equal(["-c", "4"], ping.Action.Arguments);
        Assert.NotNull(ping.Parameter);
    }

    /// <summary>The typed host is appended last, so a parameterised row has to be one whose command
    /// takes its target last — and it is the only row on the table that takes input at all.</summary>
    [Fact]
    public void Entries_PingIsTheOnlyRowThatTakesInput() {
        var parameterised = Assert.Single(Entries, e => e.Parameter is not null);

        Assert.Equal("ping <host>", parameterised.Command);
        Assert.Equal("ping -c 4 8.8.8.8", parameterised.Action.WithArgument("8.8.8.8").CommandLine);
    }

    /// <summary>Folder rows are written in the notation a user would type; expansion is
    /// <see cref="ToolkitPaths"/>'s job at run time. A row starting with anything else would be a bare
    /// name resolved off the PATH, which is not what a folder row means.</summary>
    [Fact]
    public void Folders_AreWrittenAsHomeShorthandOrAnAbsolutePath() {
        Assert.All(Entries.Where(e => e.Category == ToolkitCategory.Folders),
                   entry => Assert.True(entry.Action.Target.StartsWith('~') ||
                                        entry.Action.Target.StartsWith('/')));
    }

    /// <summary>Every folder row must be one the app's own File Explorer can reach — a Folders section
    /// whose rows only open the desktop file manager would be missing half its point. Asked through
    /// <see cref="ToolkitPaths"/>' platform seam, so a Windows host proves it for the Linux table
    /// instead of judging "~/.config" by Windows' rules and finding it unrooted.</summary>
    [Fact]
    public void Folders_CanAllBeOpenedInTheAppsOwnExplorer() {
        Assert.All(Entries.Where(e => e.IsPathEntry),
                   entry => Assert.True(
                       ToolkitPaths.IsFileSystemPath(entry.Action.Target, windows: false),
                       entry.Command));
    }

    /// <summary>The systemd rows are captured into the log, so their output is redirected and the pager
    /// must never engage — a paged command would block until the timeout killed it.</summary>
    [Fact]
    public void SystemdRows_SuppressThePager() {
        var systemd = Entries
            .Where(e => e.Action.Target is "systemctl" or "journalctl")
            .ToList();

        Assert.Equal(3, systemd.Count);
        Assert.All(systemd, entry => Assert.Contains("--no-pager", entry.Action.Arguments));
    }

    /// <summary>The XDG row is written out in full because <c>basedir-spec/latest</c> redirects to a
    /// plain-http address, which the runner refuses — the shorter URL would ship as a dead button. The
    /// https rule itself is one of the shared invariants.</summary>
    [Fact]
    public void Docs_AvoidTheUrlThatRedirectsToPlainHttp() {
        Assert.Contains(Entries, e => e.Action.Target == "https://specifications.freedesktop.org/basedir/latest/");
        Assert.DoesNotContain(Entries, e => e.Action.Target.Contains("basedir-spec", StringComparison.Ordinal));
    }

    /// <summary>The page over the Linux table draws the same way the Windows one does — proven here
    /// rather than on a Linux host, so the Windows CI leg covers it too.</summary>
    [Fact]
    public void Page_OverTheLinuxCatalog_ShowsItsRows() {
        var page = new ToolkitViewModel(LinuxToolkitCatalog.Instance);

        Assert.True(page.HasCommands);
        Assert.NotEmpty(page.Groups);
        Assert.Equal(Entries.Count, page.AllEntries.Count);
        Assert.Contains(page.AllEntries, e => e.Command == "ip addr");
        Assert.DoesNotContain(page.AllEntries, e => e.Command == "%appdata%");
    }
}
