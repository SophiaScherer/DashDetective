namespace DashDetective.Shared.Shortcuts;

/// <summary>
/// Every action a keyboard shortcut can trigger. The shell and the pages dispatch on ids rather than
/// on keys, so rebinding a gesture only ever touches <see cref="ShortcutCatalog"/>.
/// </summary>
public enum ShortcutId {
    // ----- Global navigation -----
    // NavigateTab1..8 must stay contiguous and in order: the shell maps them to nav positions by
    // subtracting NavigateTab1 (see MainWindowViewModel.HandleGlobal).
    NavigateTab1,
    NavigateTab2,
    NavigateTab3,
    NavigateTab4,
    NavigateTab5,
    NavigateTab6,
    NavigateTab7,
    NavigateTab8,
    NextTab,
    PreviousTab,
    ToggleNavCollapse,
    OpenSettings,

    // ----- Toolbar actions -----
    ToggleLive,
    Refresh,
    Export,
    ShowHelp,

    /// <summary>Puts the caret in the toolbar's universal-search box, from any tab.</summary>
    FocusSearch,

    /// <summary>Context-sensitive dismissal: closes the Help modal, else cancels whatever the current
    /// page has open, else dismisses the resource-alert banner.</summary>
    Escape,

    /// <summary>Context-sensitive activation: confirms an open dialog, else opens the selected item.
    /// Each page decides what it means, the way <see cref="Escape"/> does.</summary>
    Activate,

    /// <summary>Fills the focused field with the completion ghosted after the caret. Bound globally and
    /// claimed only by whichever field is actually showing one, so Tab still moves focus otherwise.</summary>
    AcceptCompletion,

    // ----- Search results -----

    /// <summary>Moves the highlight down the universal-search result list.</summary>
    SelectNextResult,

    /// <summary>Moves the highlight up the universal-search result list.</summary>
    SelectPreviousResult,

    // ----- Page actions -----
    FocusFilter,
    EndTask,
    SortAscending,
    SortDescending,
    NavigateBack,
    NavigateForward,
    NavigateUp,
    FocusAddressBar,
    PreviousPage,
    NextPage,

    // ----- Appearance -----
    ToggleTheme,
}
