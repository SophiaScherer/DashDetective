using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// What running an entry actually does: a target plus the arguments it takes, and the path it runs down.
/// Authored in <see cref="ToolkitCatalog"/> through the static factories below, never built from user
/// input — this is the type that makes the Toolkit's safety property structural rather than a
/// convention. <see cref="Arguments"/> is a **list**, and <see cref="ToolkitRunner"/> passes it to
/// <c>ProcessStartInfo.ArgumentList</c>, so nothing is ever concatenated into a command line and there
/// is no quoting or interpolation to get wrong.
///
/// The one variable slot in the whole feature is a parameterised entry's argument (ping/tracert), and it
/// is validated before it reaches <see cref="WithArgument"/>.
/// </summary>
public sealed record ToolkitAction {
    /// <summary>How long a captured command may run before it is killed. Generous because
    /// <c>systeminfo</c> routinely takes several seconds on a cold run.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private ToolkitAction(
        ToolkitActionKind kind, string target, IReadOnlyList<string> arguments, TimeSpan timeout) {
        Kind = kind;
        Target = target;
        Arguments = arguments;
        Timeout = timeout;
    }

    public ToolkitActionKind Kind { get; }

    /// <summary>The path, URL or executable. Environment variables are expanded by the runner at run
    /// time, not here, so a catalog built at startup doesn't bake in one session's values.</summary>
    public string Target { get; }

    /// <summary>The arguments, already split. Passed through as a list, never joined.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>The cap on a captured run. Ignored by every other kind.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// What actually ran, for the Execution Log's "$" line. Built from the target and arguments rather
    /// than from the row's label, so the log is honest about two things the label hides: the value a
    /// parameterised row's placeholder stood for, and any flags the row does not spell out.
    ///
    /// Display only — nothing is ever executed from this string.
    /// </summary>
    public string CommandLine =>
        Arguments.Count == 0 ? Target : Target + " " + string.Join(' ', Arguments);

    /// <summary>Whether this action's output is redirected into the Execution Log.</summary>
    public bool CapturesOutput => Kind == ToolkitActionKind.Capture;

    /// <summary>Whether running this raises the UAC prompt.</summary>
    public bool RequiresElevation => Kind == ToolkitActionKind.Elevated;

    /// <summary>Opens a folder (or a <c>shell:</c> location) in Explorer.</summary>
    public static ToolkitAction OpenPath(string path) =>
        new(ToolkitActionKind.OpenPath, path, [], DefaultTimeout);

    /// <summary>Opens a documentation URL in the default browser. The runner refuses anything that is
    /// not <c>https://</c>, so a typo here cannot hand an arbitrary scheme to the shell.</summary>
    public static ToolkitAction OpenUrl(string url) =>
        new(ToolkitActionKind.OpenUrl, url, [], DefaultTimeout);

    /// <summary>Starts a tool in its own window (a snap-in, a control panel, an app).</summary>
    public static ToolkitAction Launch(string fileName, params string[] arguments) =>
        new(ToolkitActionKind.Launch, fileName, arguments, DefaultTimeout);

    /// <summary>Runs a console command with its output captured into the Execution Log.</summary>
    public static ToolkitAction Capture(string fileName, params string[] arguments) =>
        new(ToolkitActionKind.Capture, fileName, arguments, DefaultTimeout);

    /// <summary>Runs a command elevated, raising the UAC prompt. Output is **not** captured — Windows
    /// forbids redirecting the streams of a process started through <c>runas</c>.</summary>
    public static ToolkitAction Elevated(string fileName, params string[] arguments) =>
        new(ToolkitActionKind.Elevated, fileName, arguments, DefaultTimeout);

    /// <summary>The same action with a different timeout, for a command known to be slow.</summary>
    public ToolkitAction WithTimeout(TimeSpan timeout) =>
        new(Kind, Target, Arguments, timeout);

    /// <summary>The same action with one more argument appended — how a parameterised entry binds the
    /// value the user supplied. The value becomes a single list element, so it can never split into a
    /// second argument or a flag however it is spelled.</summary>
    public ToolkitAction WithArgument(string argument) =>
        new(Kind, Target, [.. Arguments, argument], Timeout);
}
