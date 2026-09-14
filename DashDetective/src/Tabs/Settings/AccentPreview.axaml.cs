using Avalonia.Automation.Peers;
using Avalonia.Controls;
using System.Collections.Generic;

namespace DashDetective.Tabs.Settings;

/// <summary>One preview strip for the Accent color row. Holds no state; its colors come from the
/// enclosing <see cref="AccentPreviewScope"/>.</summary>
public partial class AccentPreview : UserControl {
    public AccentPreview() {
        InitializeComponent();
    }

    /// <summary>A screen reader meets one named picture, not a second set of dead Apply buttons.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new PreviewPeer(this);

    private sealed class PreviewPeer(Control owner) : ControlAutomationPeer(owner) {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => [];
    }
}
