using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The "+ Add command" form: closed to a single button until it is opened, then a small inline panel
/// above the command list.
///
/// It owns nothing but what has been typed. Whether a command is acceptable is
/// <see cref="ToolkitCommandValidator"/>'s to say, what it becomes is
/// <see cref="ToolkitCommandFactory"/>'s, and where it is stored is the page's — this only collects the
/// fields, asks, and hands the answer on. The two callbacks are the seam, the way
/// <c>ToolkitSearchProvider</c> takes its entries and its reveal.
/// </summary>
public partial class ToolkitCommandFormViewModel : ObservableObject {
    private readonly Func<IReadOnlyList<ToolkitEntry>> _existing;
    private readonly Action<ToolkitCommand, ToolkitCommand?> _submit;

    /// <summary>The command being edited, or null when the form is adding a new one. Held rather than
    /// inferred from the fields: a rename changes every one of them, so there would be nothing left to
    /// recognise the original by.</summary>
    private ToolkitCommand? _editing;

    /// <param name="existing">Every row already on the page, for the duplicate-title check.</param>
    /// <param name="submit">Takes a validated command and the one it replaces, if any, and puts it on
    /// the page.</param>
    public ToolkitCommandFormViewModel(
        Func<IReadOnlyList<ToolkitEntry>> existing, Action<ToolkitCommand, ToolkitCommand?> submit) {
        _existing = existing;
        _submit = submit;

        var types = new List<ToolkitCommandTypeOption>();
        foreach (var type in Enum.GetValues<ToolkitCommandType>())
            types.Add(new ToolkitCommandTypeOption(type, SelectType));

        Types = types;
        types[0].IsSelected = true;

        // "None" first, then the authored sections. Custom is left out deliberately: every command from
        // this form is already in that section, and offering it would read as a choice that does nothing.
        var categories = new List<ToolkitCategoryOption> { new("None", null, SelectCategory) };
        foreach (var category in ToolkitCatalog.Categories)
            if (category != ToolkitCategory.Custom)
                categories.Add(new ToolkitCategoryOption(
                    ToolkitCatalog.HeaderFor(category), category, SelectCategory));

        Categories = categories;
        categories[0].IsSelected = true;
    }

    /// <summary>Whether the form is showing. Closed, the page offers a single "+ Add command" button.</summary>
    [ObservableProperty] private bool _isOpen;

    /// <summary>Whether the form is changing a command rather than making one. Only the wording differs
    /// — the fields, the rules and the chips are the same either way.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Heading), nameof(SubmitLabel))]
    private bool _isEditing;

    /// <summary>The panel's title.</summary>
    public string Heading => IsEditing ? "Edit command" : "New command";

    /// <summary>What the confirming button reads.</summary>
    public string SubmitLabel => IsEditing ? "Save changes" : "Add command";

    /// <summary>The row's label, and its identity for pins and search reveal.</summary>
    [ObservableProperty] private string _title = "";

    /// <summary>The one-line subtitle. The only optional field.</summary>
    [ObservableProperty] private string _description = "";

    /// <summary>The path, program or address the chosen type calls for.</summary>
    [ObservableProperty] private string _payload = "";

    /// <summary>The arguments as one string; split on the way to the OS, never joined into a command
    /// line. Meaningless for a folder or a URL, and the field is hidden for those.</summary>
    [ObservableProperty] private string _arguments = "";

    /// <summary>Why the last attempt to save was refused, or empty. Shown under the fields — the
    /// Execution Log answers for commands that ran, and nothing has run yet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _error = "";

    public bool HasError => Error.Length > 0;

    /// <summary>The type picker's chips.</summary>
    public IReadOnlyList<ToolkitCommandTypeOption> Types { get; }

    /// <summary>The optional "also file it under" chips, "None" first.</summary>
    public IReadOnlyList<ToolkitCategoryOption> Categories { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PayloadLabel), nameof(PayloadPrompt), nameof(TakesArguments))]
    private ToolkitCommandType _selectedType;

    /// <summary>The category the command should also appear under, or null.</summary>
    [ObservableProperty] private ToolkitCategory? _selectedCategory;

    /// <summary>What the payload field is called, which is the whole difference between the types as far
    /// as the form is concerned. A plain string rather than a converter, as everywhere else here.</summary>
    public string PayloadLabel => SelectedType switch {
        ToolkitCommandType.FolderPath => "Folder path",
        ToolkitCommandType.Url => "Address",
        _ => "Program",
    };

    /// <summary>The placeholder in the payload box.</summary>
    public string PayloadPrompt => SelectedType switch {
        ToolkitCommandType.FolderPath => @"C:\work, or %appdata%\Something",
        ToolkitCommandType.Url => "https://…",
        ToolkitCommandType.Launch => "notepad, or a full path to a program",
        _ => "netstat",
    };

    /// <summary>Whether the arguments field applies. A folder and a URL take none.</summary>
    public bool TakesArguments =>
        SelectedType is ToolkitCommandType.Launch or ToolkitCommandType.Capture;

    /// <summary>Opens the form on an empty set of fields.</summary>
    [RelayCommand]
    private void Open() {
        Reset();
        IsOpen = true;
    }

    /// <summary>Closes the form and throws away what was typed.</summary>
    [RelayCommand]
    private void Cancel() {
        Reset();
        IsOpen = false;
    }

    /// <summary>Opens the form on an existing command, ready to be changed. The fields are filled from
    /// what the user originally typed rather than from the action derived from it, which is why
    /// <see cref="ToolkitCommand"/> is what gets persisted.</summary>
    public void Edit(ToolkitCommand command) {
        Reset();

        Title = command.Title;
        Description = command.Description;
        Payload = command.Payload;
        Arguments = command.Arguments;
        SelectType(FindType(command.Type));
        SelectCategory(FindCategory(command.Category));

        // Set after the fields, because filling them clears the error and would clear this too if the
        // order were reversed.
        _editing = command;
        IsEditing = true;
        IsOpen = true;
    }

    /// <summary>Validates what was typed and, if it holds up, puts it on the page. A refusal leaves every
    /// field where it is: the user is one correction away, not one re-type away.</summary>
    [RelayCommand]
    private void Save() {
        var command = ToolkitCommandValidator.Normalize(Current());

        if (ToolkitCommandValidator.Validate(command, _existing(), _editing) is { } refusal) {
            Error = refusal;
            return;
        }

        var replacing = _editing;
        Reset();
        IsOpen = false;
        _submit(command, replacing);
    }

    /// <summary>What has been typed, as a command. Not yet normalized or checked.</summary>
    private ToolkitCommand Current() =>
        new(Title, Description, SelectedType, Payload, TakesArguments ? Arguments : "",
            SelectedCategory);

    private void Reset() {
        Title = "";
        Description = "";
        Payload = "";
        Arguments = "";
        Error = "";
        _editing = null;
        IsEditing = false;
        SelectType(Types[0]);
        SelectCategory(Categories[0]);
    }

    private ToolkitCommandTypeOption FindType(ToolkitCommandType type) {
        foreach (var option in Types)
            if (option.Type == type)
                return option;

        return Types[0];
    }

    private ToolkitCategoryOption FindCategory(ToolkitCategory? category) {
        foreach (var option in Categories)
            if (option.Category == category)
                return option;

        // A category this build no longer offers degrades to "None" rather than losing the edit.
        return Categories[0];
    }

    private void SelectType(ToolkitCommandTypeOption option) {
        foreach (var candidate in Types)
            candidate.IsSelected = ReferenceEquals(candidate, option);

        SelectedType = option.Type;
    }

    private void SelectCategory(ToolkitCategoryOption option) {
        foreach (var candidate in Categories)
            candidate.IsSelected = ReferenceEquals(candidate, option);

        SelectedCategory = option.Category;
    }

    // A refusal names one field; once that field has been touched the message is about a state that no
    // longer exists, so it goes rather than sitting there contradicting what is on screen.
    partial void OnTitleChanged(string value) => Error = "";
    partial void OnPayloadChanged(string value) => Error = "";
    partial void OnSelectedTypeChanged(ToolkitCommandType value) => Error = "";
}
