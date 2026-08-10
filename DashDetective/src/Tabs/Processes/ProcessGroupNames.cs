using System;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// The third process group's caption, which is the one piece of the tab's wording that cannot be shared
/// across platforms: <see cref="ProcessCategory.Windows"/> means "Windows process" on Windows and "system
/// process" on Linux, and a Linux user reading "Windows processes · 150" would reasonably conclude the tab
/// was broken.
///
/// The enum member keeps its name — renaming it would change nothing a user sees and would touch every
/// consumer. Only the two display strings vary, behind an explicit <c>linux</c> parameter so both arms are
/// testable from either host, the same shape as <c>ToolkitPaths.Expand</c>.
/// </summary>
internal static class ProcessGroupNames {
    private const string WindowsWord = "Windows";
    private const string LinuxWord = "System";
    private const string HeaderSuffix = " processes";

    /// <summary>The word used in the summary breakdown ("… · 150 Windows").</summary>
    internal static string SystemLabel { get; } = LabelFor(OperatingSystem.IsLinux());

    /// <summary>The group header caption, before its count ("Windows processes · 150").</summary>
    internal static string SystemHeader { get; } = HeaderFor(OperatingSystem.IsLinux());

    internal static string LabelFor(bool linux) => linux ? LinuxWord : WindowsWord;

    internal static string HeaderFor(bool linux) => LabelFor(linux) + HeaderSuffix;
}
