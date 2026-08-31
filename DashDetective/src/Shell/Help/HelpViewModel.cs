using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using System.Collections.Generic;

namespace DashDetective.Shell.Help;

/// <summary>
/// Backs the Help modal: open/closed state, the selected tab, and the static copy from
/// <see cref="HelpContent"/>. Owned by the shell (the overlay covers the whole window, navigation bar
/// included) and opened by the navigation bar's Help button via
/// <c>NavigationViewModel.HelpRequested</c>. Session-only — nothing here is persisted.
/// </summary>
public partial class HelpViewModel : ViewModelBase {
    private readonly ShortcutBindings _shortcuts;

    /// <summary>Takes the live bindings rather than reading the catalog, so the modal lists the keys the
    /// user actually chose. Re-announces its groups when they change, since the modal may be open.</summary>
    public HelpViewModel(ShortcutBindings shortcuts) {
        _shortcuts = shortcuts;
        _shortcuts.Changed += () => OnPropertyChanged(nameof(ShortcutGroups));

        Tabs = [
            new("Overview", HelpTab.Overview, SelectTab),
            new("Getting started", HelpTab.GettingStarted, SelectTab),
            new("Tips", HelpTab.Tips, SelectTab),
            new("Shortcuts", HelpTab.Shortcuts, SelectTab),
        ];
        SyncTabs();
    }

    /// <summary>Whether the modal is showing. Drives the overlay's visibility.</summary>
    [ObservableProperty] private bool _isOpen;

    /// <summary>Which section is showing. Overview shows all of them.</summary>
    [ObservableProperty] private HelpTab _selectedTab;

    /// <summary>The tab strip's entries, in display order.</summary>
    public IReadOnlyList<HelpTabOption> Tabs { get; }

    /// <summary>Whether each section renders. Overview is a filter rather than a section, so it turns
    /// every one on and the markup is written once.</summary>
    public bool ShowGettingStarted => SelectedTab is HelpTab.Overview or HelpTab.GettingStarted;

    /// <inheritdoc cref="ShowGettingStarted"/>
    public bool ShowTips => SelectedTab is HelpTab.Overview or HelpTab.Tips;

    /// <inheritdoc cref="ShowGettingStarted"/>
    public bool ShowShortcuts => SelectedTab is HelpTab.Overview or HelpTab.Shortcuts;

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

    /// <summary>Shows the modal, always on Overview: opening Help is a request for everything it knows,
    /// not for wherever the last visit left off.</summary>
    [RelayCommand]
    public void Open() {
        SelectedTab = HelpTab.Overview;
        IsOpen = true;
    }

    /// <summary>Hides the modal (the ×, the Esc key, and a click on the scrim all land here).</summary>
    [RelayCommand]
    public void Close() => IsOpen = false;

    private void SelectTab(HelpTabOption option) => SelectedTab = option.Value;

    // Section visibility is derived, so the setter has to announce it; the strip's selected state is a
    // flag per option, so that has to be pushed.
    partial void OnSelectedTabChanged(HelpTab value) {
        SyncTabs();
        OnPropertyChanged(nameof(ShowGettingStarted));
        OnPropertyChanged(nameof(ShowTips));
        OnPropertyChanged(nameof(ShowShortcuts));
    }

    private void SyncTabs() {
        foreach (var tab in Tabs)
            tab.IsSelected = tab.Value == SelectedTab;
    }
}
