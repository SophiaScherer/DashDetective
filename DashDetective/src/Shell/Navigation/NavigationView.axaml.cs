using Avalonia.Controls;

namespace DashDetective.Shell.Navigation;

/// <summary>
/// The shell's navigation bar. A self-contained component bound to a <see cref="NavigationViewModel"/>;
/// embedded directly by the shell rather than routed through the <c>ViewLocator</c>.
/// </summary>
public partial class NavigationView : UserControl {
    public NavigationView() {
        InitializeComponent();
    }
}
