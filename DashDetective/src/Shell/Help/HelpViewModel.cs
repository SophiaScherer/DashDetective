using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using System.Collections.Generic;

namespace DashDetective.Shell.Help;

/// <summary>
/// Backs the Help modal: open/closed state plus the static copy from <see cref="HelpContent"/>.
/// Owned by the shell (the overlay covers the whole window, navigation bar included) and opened by
/// the navigation bar's Help button via <c>NavigationViewModel.HelpRequested</c>. Session-only —
/// nothing here is persisted.
/// </summary>
public partial class HelpViewModel : ViewModelBase {
    private readonly ShortcutBindings _shortcuts;

    /// <summary>Takes the live bindings rather than reading the catalog, so the modal lists the keys the
    /// user actually chose. Re-announces its groups when they change, since the modal may be open.</summary>
    public HelpViewModel(ShortcutBindings shortcuts) {
        _shortcuts = shortcuts;
        _shortcuts.Changed += () => OnPropertyChanged(nameof(ShortcutGroups));
    }

    /// <summary>Whether the modal is showing. Drives the overlay's visibility.</summary>
    [ObservableProperty] private bool _isOpen;

    /// <summary>The one-paragraph app description shown above the tour.</summary>
    public string Description => HelpContent.Description;

    /// <summary>The page-by-page tour, in navigation order.</summary>
    public IReadOnlyList<HelpTopic> GettingStarted => HelpContent.GettingStarted;

    /// <summary>The orientation tips, in display order.</summary>
    public IReadOnlyList<HelpTopic> Tips => HelpContent.Tips;

    /// <summary>The keyboard shortcuts, grouped by where they apply. Read straight from the bindings the
    /// key handler uses, so this page always describes what the keys actually do — rebinds included.</summary>
    public IReadOnlyList<ShortcutGroup> ShortcutGroups => _shortcuts.HelpGroups;

    /// <summary>Product name and version for the modal's subheading, read from the running assembly
    /// rather than hard-coded (same source as the Settings footer).</summary>
    public string VersionText => $"{AppInfo.Name} · v{AppInfo.Version}";

    /// <summary>Shows the modal.</summary>
    [RelayCommand]
    public void Open() => IsOpen = true;

    /// <summary>Hides the modal (the ×, the Esc key, and a click on the scrim all land here).</summary>
    [RelayCommand]
    public void Close() => IsOpen = false;
}
