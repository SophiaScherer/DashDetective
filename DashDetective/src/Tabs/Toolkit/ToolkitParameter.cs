using CommunityToolkit.Mvvm.ComponentModel;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The one editable slot a parameterised row carries — the host on <c>ping</c> and <c>tracert</c>. A
/// small item view model, the <see cref="ToolkitCategoryOption"/> shape, because the text box binds
/// <see cref="Value"/> two-way.
///
/// It holds the typed value and nothing else: what counts as usable is
/// <see cref="ToolkitHostValidator"/>'s to say, and appending it to the command is
/// <see cref="ToolkitAction.WithArgument"/>'s.
/// </summary>
public partial class ToolkitParameter : ObservableObject {
    public ToolkitParameter(string prompt) {
        Prompt = prompt;
    }

    /// <summary>Placeholder text naming what the box wants ("host or IP").</summary>
    public string Prompt { get; }

    /// <summary>What the user typed. Survives a filter change and a tab switch, so a host entered once
    /// stays put for the next run.</summary>
    [ObservableProperty] private string _value = "";

    /// <summary>Fills the box with a suggested default, but only while the user has not typed anything
    /// — the gateway lookup finishes after the page is already on screen, and must never overwrite a
    /// host somebody is part-way through entering.</summary>
    public void SeedIfEmpty(string suggestion) {
        if (string.IsNullOrWhiteSpace(Value) && !string.IsNullOrWhiteSpace(suggestion))
            Value = suggestion;
    }
}
