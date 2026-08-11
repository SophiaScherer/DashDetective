using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
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
/// process itself, and only ever runs actions authored in an <see cref="IToolkitCatalog"/> or built
/// from what the user typed into the form.
/// </summary>
public partial class ToolkitViewModel : ViewModelBase, ISelfScrollingPage, IShortcutTarget {
    private readonly ToolkitRunner _runner = new();
    private readonly IToolkitCatalog _catalog;
    private ToolkitCategory? _category;
    private string? _pendingReveal;

    public ToolkitViewModel() : this(IToolkitCatalog.ForCurrentPlatform()) { }

    /// <summary>Test seam: the same page over an explicit command set. The public ctor resolves this
    /// machine's, so the shell still builds this with <c>new()</c>.</summary>
    internal ToolkitViewModel(IToolkitCatalog catalog) {
        _catalog = catalog;

        var options = new List<ToolkitCategoryOption> {
            new("All", null, SelectCategory),
        };
        foreach (var category in ToolkitCatalog.Categories)
            options.Add(new ToolkitCategoryOption(ToolkitCatalog.HeaderFor(category), category, SelectCategory));

        Categories = options;
        options[0].IsSelected = true;
        Form = new ToolkitCommandFormViewModel(() => AllEntries, ApplyFromForm);
        Log.CollectionChanged += (_, _) => HasLog = Log.Count > 0;
        RebuildGroups();

        // Not awaited: the page must be usable immediately, and this only fills in a suggestion. Started
        // on the UI thread, so the continuation that writes the boxes comes back to it.
        _ = SeedParameterDefaultsAsync();
    }

    /// <summary>Suggests the machine's default gateway in the parameterised rows' boxes — the
    /// convenience the design asks for, and nothing more: the box stays editable, and a value the user
    /// has already typed is never overwritten. Adapter enumeration is slow enough to keep off the UI
    /// thread.</summary>
    private async Task SeedParameterDefaultsAsync() {
        var gateway = await Task.Run(ToolkitDefaults.PrimaryGateway).ConfigureAwait(true);
        foreach (var entry in _catalog.Entries)
            entry.Parameter?.SeedIfEmpty(gateway);
    }

    /// <summary>The filter chips, "All" first and then the catalog's categories in display order.</summary>
    public IReadOnlyList<ToolkitCategoryOption> Categories { get; }

    /// <summary>The command list as the filter leaves it: one section per non-empty category.</summary>
    public ObservableCollection<ToolkitGroup> Groups { get; } = [];

    /// <summary>The rows the user authored, in the order they were added.</summary>
    public ObservableCollection<ToolkitEntry> Custom { get; } = [];

    /// <summary>The "+ Add command" form. Always present, but closed to a single button until it is
    /// opened.</summary>
    public ToolkitCommandFormViewModel Form { get; }

    /// <summary>Every row the page knows about — this platform's built-in rows, then the user's. This,
    /// not <see cref="IToolkitCatalog.Entries"/>, is what the filter, the pins and universal search
    /// work off. Rebuilt on read: the list is small, and a cached copy would be one more thing to keep
    /// in step with <see cref="Custom"/>.</summary>
    public IReadOnlyList<ToolkitEntry> AllEntries {
        get {
            var all = new List<ToolkitEntry>(_catalog.Entries.Count + Custom.Count);
            all.AddRange(_catalog.Entries);
            all.AddRange(Custom);
            return all;
        }
    }

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

    /// <summary>Raised when a row asks to be opened in the app's own File Explorer, carrying the resolved
    /// path. The composition root binds it to the same jump universal search uses — the page itself has no
    /// idea another tab exists, exactly as <see cref="PinsChanged"/> has no idea a settings file
    /// does.</summary>
    public event Action<string>? FileExplorerRevealRequested;

    /// <summary>
    /// Activates a row. A row that names a place on disk opens in the app's own File Explorer — staying
    /// in the app is the point of having the tab — and the shell is still one click away on the row's
    /// other icon. Everything else runs.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task Run(ToolkitEntry? entry) {
        if (entry is { CanOpenInApp: true }) {
            OpenInApp(entry);
            return Task.CompletedTask;
        }

        return Execute(entry);
    }

    /// <summary>Opens the row's folder in the app's own File Explorer. Nothing is logged: the Execution
    /// Log is a transcript of what ran, and this navigates rather than running anything.</summary>
    [RelayCommand]
    private void OpenInApp(ToolkitEntry? entry) {
        if (entry is not { CanOpenInApp: true })
            return;

        FileExplorerRevealRequested?.Invoke(entry.ResolvedPath);
    }

    /// <summary>Opens the row's location in Windows Explorer — the row's original behaviour, kept as its
    /// own icon now that the click goes to the in-app explorer. Goes through the ordinary run path, so
    /// the Execution Log still records it.</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task OpenExternally(ToolkitEntry? entry) => Execute(entry);

    /// <summary>
    /// Runs a command row and records the outcome in the Execution Log, newest first. The commands that
    /// reach here (<see cref="RunCommand"/>, <see cref="OpenExternallyCommand"/>) refuse concurrent runs
    /// rather than queueing them: a second click while one is in flight would interleave two stanzas, and
    /// the log reads as a transcript. Refusing them also disables every row's button for the duration (the
    /// generated command reports <c>CanExecute</c> false while it runs), which is the page's whole busy
    /// state — no separate flag to keep in step.
    ///
    /// The stanza is written **before** the command runs and replaced in place when it finishes, so a
    /// slow capture reads as in-flight rather than as a dead button. That is how a terminal transcript
    /// behaves anyway, which is why this needs no spinner of its own.
    ///
    /// Nothing here interprets the command — <see cref="ToolkitRunner"/> already returns display-ready
    /// text for every outcome, including the failures.
    /// </summary>
    private async Task Execute(ToolkitEntry? entry) {
        if (entry is null)
            return;

        // Stamped once, so the stanza keeps the time the command was *started* rather than jumping to
        // the time it finished — which for a 90 s systeminfo would be a minute and a half out.
        var time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        // A parameterised row is rejected before anything starts if what was typed is not a host. The
        // rejection is logged rather than shown inline, so the answer is where every other answer is.
        if (entry.Parameter is { } parameter && !ToolkitHostValidator.IsValid(parameter.Value)) {
            Log.Insert(0, new ToolkitLogEntry(
                time, entry.Command, ToolkitOutputFormatter.InvalidHost(parameter.Value)));
            return;
        }

        var action = Bind(entry);

        // The "$" line shows the resolved command line, not the row's label: a parameterised label is a
        // placeholder ("ping <host>"), and a row may carry flags it does not spell out.
        var line = action.CommandLine;
        var pending = new ToolkitLogEntry(time, line, ToolkitOutputFormatter.Running);
        Log.Insert(0, pending);

        var result = await _runner.RunAsync(action);

        // Reference equality, not the record's value equality: if the user cleared the log while the
        // command was in flight, the placeholder is gone and the result goes with it — they asked for an
        // empty log, so putting a stanza back would be ignoring them.
        if (Log.Count > 0 && ReferenceEquals(Log[0], pending))
            Log[0] = new ToolkitLogEntry(time, line, result.Output);
    }

    /// <summary>
    /// What the row's copy button puts on the clipboard: the same resolved command line the Execution
    /// Log would record, so what is pasted is what would have run — not the row's placeholder label, and
    /// for a documentation row the URL rather than its title.
    /// </summary>
    public string CopyTextFor(ToolkitEntry entry) => Bind(entry).CommandLine;

    /// <summary>Binds a parameterised row's typed value onto its action. An unusable value is left off
    /// altogether rather than appended blank, so copying a half-filled row gives a clean command line
    /// instead of one with a dangling argument.</summary>
    private static ToolkitAction Bind(ToolkitEntry entry) {
        if (entry.Parameter is not { } parameter)
            return entry.Action;

        var host = ToolkitHostValidator.Normalize(parameter.Value);
        return host.Length == 0 ? entry.Action : entry.Action.WithArgument(host);
    }

    /// <summary>Raised when a pin is added or removed, so the composition root can persist it. Carries
    /// nothing — the encoding is <see cref="EncodePins"/>'s to produce.</summary>
    public event Action? PinsChanged;

    /// <summary>Pins or unpins a command, lifting it into (or dropping it back out of) the Pinned
    /// section. The list is rebuilt because the row physically moves between sections.</summary>
    [RelayCommand]
    private void TogglePin(ToolkitEntry? entry) {
        if (entry is null)
            return;

        entry.IsPinned = !entry.IsPinned;
        RebuildGroups();
        PinsChanged?.Invoke();
    }

    /// <summary>The pinned commands as one persistable string, in list order.</summary>
    public string EncodePins() {
        var commands = new List<string>();
        foreach (var entry in AllEntries)
            if (entry.IsPinned)
                commands.Add(entry.Command);

        return ToolkitPins.Encode(commands);
    }

    /// <summary>Applies persisted pins at startup. A pin naming a command that no longer exists is simply
    /// dropped — pins are stored by command text precisely so a changed list cannot re-point them at
    /// something else. Custom commands must be loaded first, or a pin naming one finds nothing.</summary>
    public void LoadPins(string? encoded) {
        var pinned = new HashSet<string>(ToolkitPins.Decode(encoded), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in AllEntries)
            entry.IsPinned = pinned.Contains(entry.Command);

        RebuildGroups();
    }

    /// <summary>Raised when the user adds, edits or removes one of their own commands, so the composition
    /// root can persist it. Carries nothing — the encoding is <see cref="EncodeCommands"/>'s to
    /// produce, exactly as with <see cref="PinsChanged"/>.</summary>
    public event Action? CommandsChanged;

    /// <summary>Adds a command the user authored and announces it for persistence. The caller has already
    /// put it past <see cref="ToolkitCommandValidator"/>.</summary>
    public void AddCommand(ToolkitCommand command) {
        Custom.Add(ToolkitCommandFactory.ToEntry(command));
        RebuildGroups();
        CommandsChanged?.Invoke();
    }

    /// <summary>Replaces one of the user's commands with an edited version, in place. The row keeps its
    /// position and its pin: a rename is still the same command as far as the user is concerned, and
    /// re-encoding the pins afterwards writes the new title, because <see cref="EncodePins"/> reads live
    /// state rather than a stored key.</summary>
    public void UpdateCommand(ToolkitCommand original, ToolkitCommand edited) {
        for (var i = 0; i < Custom.Count; i++) {
            if (!ReferenceEquals(Custom[i].Source, original))
                continue;

            var replacement = ToolkitCommandFactory.ToEntry(edited);
            replacement.IsPinned = Custom[i].IsPinned;
            Custom[i] = replacement;
            RebuildGroups();
            CommandsChanged?.Invoke();
            return;
        }
    }

    /// <summary>Removes one of the user's commands. Its pin, if it had one, goes with it on the next
    /// encode — the same way a catalog row that disappears drops its pin.</summary>
    public void RemoveCommand(ToolkitEntry entry) {
        if (!Custom.Remove(entry))
            return;

        RebuildGroups();
        CommandsChanged?.Invoke();
    }

    /// <summary>Opens the form on one of the user's commands. Only their own rows offer this — a catalog
    /// row has no <see cref="ToolkitEntry.Source"/> to fill the fields from.</summary>
    [RelayCommand]
    private void EditCustom(ToolkitEntry? entry) {
        if (entry?.Source is { } source)
            Form.Edit(source);
    }

    /// <summary>Deletes one of the user's commands from its row.</summary>
    [RelayCommand]
    private void DeleteCustom(ToolkitEntry? entry) {
        if (entry is { IsCustom: true })
            RemoveCommand(entry);
    }

    // Where the form's Save lands: an append, or a replacement of the command being edited.
    private void ApplyFromForm(ToolkitCommand command, ToolkitCommand? replacing) {
        if (replacing is null)
            AddCommand(command);
        else
            UpdateCommand(replacing, command);
    }

    /// <summary>The user's own commands as one persistable string.</summary>
    public string EncodeCommands() {
        var commands = new List<ToolkitCommand>();
        foreach (var entry in Custom)
            if (entry.Source is { } source)
                commands.Add(source);

        return ToolkitCommandCodec.Encode(commands);
    }

    /// <summary>Applies persisted commands at startup, replacing whatever is there. Announces nothing:
    /// restoring what was saved is not a change to save back.</summary>
    public void LoadCommands(string? encoded) {
        Custom.Clear();
        foreach (var command in ToolkitCommandCodec.Decode(encoded))
            Custom.Add(ToolkitCommandFactory.ToEntry(command));

        RebuildGroups();
    }

    /// <summary>Empties the search box (its × button, and Esc while it has content).</summary>
    [RelayCommand]
    private void ClearSearch() => Search = "";

    /// <summary>Empties the Execution Log (its "Clear" button).</summary>
    [RelayCommand]
    private void ClearLog() => Log.Clear();

    /// <summary>
    /// The Execution Log as plain text, for the "Export" button. Stanzas keep the order they are shown
    /// in — newest first — so the file reads as what was on screen rather than quietly reversing it; the
    /// timestamps make the direction unambiguous either way.
    ///
    /// Built here and written by the view code-behind, which owns the save dialog (it needs the
    /// <c>TopLevel</c>), exactly as the Settings exports are.
    /// </summary>
    public string BuildLogText() {
        var sb = new StringBuilder();
        sb.AppendLine("DashDetective — Toolkit execution log");
        sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        sb.AppendLine();

        foreach (var entry in Log) {
            sb.AppendLine($"[{entry.Time}] $ {entry.Command}");
            sb.AppendLine(entry.Output);
            sb.AppendLine();
        }

        return sb.ToString();
    }

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

        // Counted by distinct row rather than by section total: a custom command the user filed under a
        // category shows in two sections, and the count is of commands, not of places to click one.
        var matched = new HashSet<ToolkitEntry>();
        foreach (var group in ToolkitFilter.Group(AllEntries, _category, Search)) {
            Groups.Add(group);
            foreach (var item in group.Items)
                matched.Add(item);
        }

        HasCommands = matched.Count > 0;
        CountLabel = matched.Count == 1
            ? "1 command"
            : matched.Count.ToString(CultureInfo.InvariantCulture) + " commands";
    }
}
