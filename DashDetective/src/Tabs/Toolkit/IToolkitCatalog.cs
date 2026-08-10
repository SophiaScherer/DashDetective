using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The built-in command set for this machine. The rows are the one part of the Toolkit that cannot be
/// shared: a table naming <c>taskschd.msc</c> and <c>%appdata%</c> is a table of dead buttons on Linux.
/// Everything around them — the runner, the filter, the pins, the form — is platform-neutral and stays
/// where it is.
///
/// The copy the tab is drawn from (categories, headers, badge labels) is <b>not</b> here: it reads the
/// same on every platform, so it stays on <see cref="ToolkitCatalog"/> as pure statics.
/// </summary>
internal interface IToolkitCatalog {
    /// <summary>Every built-in command, in no particular order — the list groups them by category. The
    /// user's own rows are not here; what everything downstream reads is
    /// <see cref="ToolkitViewModel.AllEntries"/>.</summary>
    IReadOnlyList<ToolkitEntry> Entries { get; }

    /// <summary>The catalog for this machine, or an empty one where there is no table to offer. An
    /// empty tab falls back to the page's own "no commands" state and the user can still author their
    /// own rows, which is a better answer than thirty rows that can only fail.</summary>
    static IToolkitCatalog ForCurrentPlatform() =>
        OperatingSystem.IsWindows()
            ? WindowsToolkitCatalog.Instance
            : UnsupportedToolkitCatalog.Instance;
}
