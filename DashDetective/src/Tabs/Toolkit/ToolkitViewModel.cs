using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The Toolkit tab ("Commands" in the design document): a browsable list of common commands for
/// navigating or diagnosing the machine, with an execution log beside it. Self-scrolling — the
/// command column and the log panel scroll independently, so the log stays pinned in view.
///
/// The list is narrowed by a category chip and a search box together, through
/// <see cref="ToolkitFilter"/>. Picking a row runs its <see cref="ToolkitEntry.Action"/> through
/// <see cref="ToolkitRunner"/> and prepends what happened to the log; the view model never touches a
/// process itself, and only ever runs actions authored in <see cref="ToolkitCatalog"/>.
/// </summary>
public partial class ToolkitViewModel : ViewModelBase, ISelfScrollingPage, IShortcutTarget {
    private readonly ToolkitRunner _runner = new();
    private ToolkitCategory? _category;
    private string? _pendingReveal;

    public ToolkitViewModel() {
        var options = new List<ToolkitCategoryOption> {
            new("All", null, SelectCategory),
        };
        foreach (var category in ToolkitCatalog.Categories)
            options.Add(new ToolkitCategoryOption(ToolkitCatalog.HeaderFor(category), category, SelectCategory));

        Categories = options;
        options[0].IsSelected = true;
        Log.CollectionChanged += (_, _) => HasLog = Log.Count > 0;
        RebuildGroups();
    }

    /// <summary>The filter chips, "All" first and then the catalog's categories in display order.</summary>
    public IReadOnlyList<ToolkitCategoryOption> Categories { get; }

    /// <summary>The command list as the filter leaves it: one section per non-empty category.</summary>
    public ObservableCollection<ToolkitGroup> Groups { get; } = [];

    /// <summary>The search box. Narrows on every keystroke, alongside the selected chip.</summary>
    [ObservableProperty] private string _search = "";

    /// <summary>How many commands the filter left, as the count beside the chips reads it.</summary>
    [ObservableProperty] private string _countLabel = "";

    /// <summary>Whether anything survived the filter. Drives the empty state.</summary>
    [ObservableProperty] private bool _hasCommands;

    /// <summary>What has been run this session, newest first. Session-only and never persisted.</summary>
    public ObservableCollection<ToolkitLogEntry> Log { get; } = [];

    /// <summary>Whether the log has anything in it. Drives its empty state.</summary>
    [ObservableProperty] private bool _hasLog;

    /// <summary>Raised when the focus-filter shortcut fires, so the view can put the caret in the
    /// search box. UI-only; carries no state — the seam the Processes filter uses.</summary>
    public event Action? SearchFocusRequested;

    /// <summary>Nudges the view to reveal the pending command. Carries nothing: the command lives in
    /// <see cref="TakePendingReveal"/>, so a reveal that arrives before the view exists is not lost.</summary>
    public event Action? RevealRequested;

    /// <summary>Brings a command into view after universal search navigated here. The filter is reset
    /// first: a chip or a half-typed search from earlier could otherwise have hidden the very row the
    /// user just picked.</summary>
    public void Reveal(string command) {
        SelectCategory(Categories[0]);
        Search = "";

        // Held rather than passed to the event, because on the first jump to this tab the view has not
        // been built yet and so is not listening — the shell navigates and reveals in one breath, but
        // the page's visual tree only appears on the next layout pass. The view drains this when it
        // attaches, and the event covers the case where it was already attached.
        _pendingReveal = command;
        RevealRequested?.Invoke();
    }

    /// <summary>Takes the command waiting to be revealed, if any, clearing it. Called by the view both
    /// when it attaches and when <see cref="RevealRequested"/> fires, so whichever comes first wins and
    /// the other finds nothing.</summary>
    internal string? TakePendingReveal() {
        var pending = _pendingReveal;
        _pendingReveal = null;
        return pending;
    }

    public ShortcutScope Scope => ShortcutScope.Toolkit;

    public bool HandleShortcut(ShortcutId id) {
        switch (id) {
            case ShortcutId.FocusFilter:
                SearchFocusRequested?.Invoke();
                return true;

            // Leave Esc unconsumed with an empty box, so the shell can still dismiss a banner with it.
            case ShortcutId.Escape when Search.Length > 0:
                ClearSearch();
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Runs a command row and records the outcome in the Execution Log, newest first. Concurrent runs
    /// are refused rather than queued: a second click while one is in flight would interleave two
    /// stanzas, and the log reads as a transcript. Refusing them also disables every row's button for
    /// the duration (the generated command reports <c>CanExecute</c> false while it runs), which is the
    /// page's whole busy state — no separate flag to keep in step.
    ///
    /// The stanza is written **before** the command runs and replaced in place when it finishes, so a
    /// slow capture reads as in-flight rather than as a dead button. That is how a terminal transcript
    /// behaves anyway, which is why this needs no spinner of its own.
    ///
    /// Nothing here interprets the command — <see cref="ToolkitRunner"/> already returns display-ready
    /// text for every outcome, including the failures.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task Run(ToolkitEntry? entry) {
        if (entry is null)
            return;

        // Stamped once, so the stanza keeps the time the command was *started* rather than jumping to
        // the time it finished — which for a 90 s systeminfo would be a minute and a half out.
        var time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        var pending = new ToolkitLogEntry(time, entry.Command, ToolkitOutputFormatter.Running);
        Log.Insert(0, pending);

        var result = await _runner.RunAsync(entry.Action);

        // Reference equality, not the record's value equality: if the user cleared the log while the
        // command was in flight, the placeholder is gone and the result goes with it — they asked for an
        // empty log, so putting a stanza back would be ignoring them.
        if (Log.Count > 0 && ReferenceEquals(Log[0], pending))
            Log[0] = new ToolkitLogEntry(time, entry.Command, result.Output);
    }

    /// <summary>Empties the search box (its × button, and Esc while it has content).</summary>
    [RelayCommand]
    private void ClearSearch() => Search = "";

    /// <summary>Empties the Execution Log (its "Clear" button).</summary>
    [RelayCommand]
    private void ClearLog() => Log.Clear();

    partial void OnSearchChanged(string value) => RebuildGroups();

    private void SelectCategory(ToolkitCategoryOption option) {
        foreach (var candidate in Categories)
            candidate.IsSelected = ReferenceEquals(candidate, option);

        _category = option.Category;
        RebuildGroups();
    }

    /// <summary>Re-runs the filter and replaces the sections wholesale. The list is small and only
    /// changes on a keystroke or a chip, so there is nothing here for a keyed diff to save.</summary>
    private void RebuildGroups() {
        Groups.Clear();
        var matched = 0;
        foreach (var group in ToolkitFilter.Group(ToolkitCatalog.Entries, _category, Search)) {
            Groups.Add(group);
            matched += group.Items.Count;
        }

        HasCommands = matched > 0;
        CountLabel = matched == 1
            ? "1 command"
            : matched.ToString(CultureInfo.InvariantCulture) + " commands";
    }
}
