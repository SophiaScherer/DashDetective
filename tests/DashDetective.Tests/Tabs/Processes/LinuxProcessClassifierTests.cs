using DashDetective.Tabs.Processes;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="LinuxProcessClassifier"/>'s rule table: the three groups, the order the rules
/// have to run in, and the hosts where the cgroup says nothing.</summary>
public class LinuxProcessClassifierTests {
    private const string AppScope =
        "/user.slice/user-1000.slice/user@1000.service/app.slice/app-gnome-firefox-3456.scope";
    private const string UserService =
        "/user.slice/user-1000.slice/user@1000.service/app.slice/gvfs-daemon.service";
    private const string SessionService =
        "/user.slice/user-1000.slice/user@1000.service/session.slice/org.gnome.Shell@wayland.service";
    private const string SystemService = "/system.slice/cron.service";

    /// <summary>Defaults describe an ordinary desktop process, so each test states only what it is
    /// about.</summary>
    private static ProcessCategory Classify(
        string cgroup, int? uid = 1000, int pid = 1234, int parentPid = 1,
        char state = 'S', bool hasCommandLine = true) =>
        LinuxProcessClassifier.Classify(pid, parentPid, state, uid, hasCommandLine, cgroup);

    // ----- Rule 1: kernel threads -----

    [Fact]
    public void Classify_KthreaddItself_IsSystem() =>
        Assert.Equal(ProcessCategory.Windows, Classify("", uid: 0, pid: 2, hasCommandLine: false));

    [Fact]
    public void Classify_ChildOfKthreadd_IsSystem() =>
        Assert.Equal(
            ProcessCategory.Windows,
            Classify("", uid: 0, pid: 58, parentPid: 2, state: 'I', hasCommandLine: false));

    /// <summary>The general test, since not every kernel thread is a direct child of kthreadd: no
    /// user-space address space means no <c>cmdline</c>.</summary>
    [Fact]
    public void Classify_NoCommandLine_IsSystem() =>
        Assert.Equal(ProcessCategory.Windows, Classify("", uid: 0, pid: 900, hasCommandLine: false));

    /// <summary>A zombie has lost its address space too, so it also has an empty <c>cmdline</c> — but it is
    /// the corpse of a user process, not a kernel thread, and its cgroup still places it. Classifying on
    /// the empty cmdline alone would file a crashed Firefox tab under System.</summary>
    [Fact]
    public void Classify_Zombie_KeepsItsOwnGroupRatherThanReadingAsAKernelThread() =>
        Assert.Equal(ProcessCategory.App, Classify(AppScope, state: 'Z', hasCommandLine: false));

    // ----- Rule 2: system processes -----

    [Fact]
    public void Classify_RootOwned_IsSystem() =>
        Assert.Equal(ProcessCategory.Windows, Classify(SystemService, uid: 0));

    /// <summary>Plenty of system units run as their own non-root user (systemd-resolve, polkitd, nginx
    /// workers). The slice is what makes them system, not the owner — which is why both are tested.</summary>
    [Fact]
    public void Classify_NonRootServiceInSystemSlice_IsStillSystem() =>
        Assert.Equal(ProcessCategory.Windows, Classify(SystemService, uid: 101));

    /// <summary>An unknown owner must not read as root. <c>ProcPidStatusParser</c> reports <c>null</c> for a
    /// denied <c>status</c> precisely so this cannot silently promote a user process.</summary>
    [Fact]
    public void Classify_UnknownOwner_IsNotTreatedAsRoot() =>
        Assert.Equal(ProcessCategory.App, Classify(AppScope, uid: null));

    // ----- Rules 3 and 4: the order that matters -----

    [Fact]
    public void Classify_AppScope_IsAnApp() =>
        Assert.Equal(ProcessCategory.App, Classify(AppScope));

    /// <summary>The rule-ordering fix. Modern systemd puts user <b>units</b> inside <c>app.slice</c>
    /// alongside the launched <c>app-*.scope</c>s, so testing for the slice before the <c>.service</c> leaf
    /// files every user daemon as a foreground app.</summary>
    [Fact]
    public void Classify_UserServiceInsideAppSlice_IsBackgroundNotAnApp() =>
        Assert.Equal(ProcessCategory.Background, Classify(UserService));

    [Fact]
    public void Classify_SessionService_IsBackground() =>
        Assert.Equal(ProcessCategory.Background, Classify(SessionService));

    /// <summary>A scope outside <c>app.slice</c> still reads as an app on its leaf alone — the shape a
    /// terminal-launched program takes.</summary>
    [Fact]
    public void Classify_AppScopeLeafOutsideAppSlice_IsAnApp() =>
        Assert.Equal(
            ProcessCategory.App, Classify("/user.slice/user-1000.slice/app-gnome-terminal-1234.scope"));

    // ----- Rule 5: nothing to go on -----

    /// <summary>A cgroup v1-only host yields no unified path, so the classifier learns nothing and must
    /// fall through rather than guess. Root is still caught by rule 2, so daemons stay correct.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("/user.slice/user-1000.slice/session-2.scope")]
    public void Classify_NothingIdentifying_IsBackground(string cgroup) =>
        Assert.Equal(ProcessCategory.Background, Classify(cgroup));
}
