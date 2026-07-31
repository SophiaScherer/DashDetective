using DashDetective.Shared;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The Toolkit tab ("Commands" in the design document): a browsable list of common commands for
/// navigating or diagnosing the machine, with an execution log beside it. Self-scrolling — the
/// command column and the log panel scroll independently, so the log stays pinned in view.
/// </summary>
public partial class ToolkitViewModel : ViewModelBase, ISelfScrollingPage {
}
