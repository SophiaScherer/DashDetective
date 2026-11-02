using Avalonia.Controls;

namespace DashDetective.Shared.Controls;

/// <summary>
/// The DashDetective mark, in the accent. One control so the title bar, the navigation bar and the accent
/// preview cannot drift apart; the caller sets its size.
/// </summary>
public partial class BrandMark : UserControl {
    public BrandMark() {
        InitializeComponent();
    }
}
