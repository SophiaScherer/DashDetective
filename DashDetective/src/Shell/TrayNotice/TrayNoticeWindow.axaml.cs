using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace DashDetective.Shell.TrayNotice;

/// <summary>
/// The one-time "this app is still running" notice, shown before the first hide-to-tray. Closing the
/// window used to leave a sampling process behind with no notice of any kind; this is that notice, and
/// it doubles as the user's chance to exit instead.
///
/// A plain window with no view model: it holds no state beyond which button was pressed.
/// </summary>
public partial class TrayNoticeWindow : Window {
    public TrayNoticeWindow() {
        InitializeComponent();
    }

    /// <summary>Asks the user, returning <c>true</c> to keep running in the tray and <c>false</c> to exit.
    /// The result is nullable so that dismissing the dialog from its title bar answers "keep running" —
    /// what the setting already says — rather than falling to <c>default(bool)</c> and exiting the app.</summary>
    public static async Task<bool> AskAsync(Window owner) =>
        await new TrayNoticeWindow().ShowDialog<bool?>(owner) ?? true;

    private void OnKeepRunningClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close(false);
}
