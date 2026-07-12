using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DashDetective.Tabs.Network;

/// <summary>
/// One item in the Active Connections pager (a page number, an ellipsis gap, or a Prev/Next arrow) —
/// the Google search-results style. Same selectable-item-VM shape as <c>FilterOption</c>: it exposes
/// immutable display state and a <see cref="SelectCommand"/> that calls back into the owning VM with
/// the target page. An ellipsis (<see cref="PageNumber"/> null) is non-clickable, and Prev/Next are
/// disabled at the ends; both are represented by <see cref="IsEnabled"/> = false.
/// </summary>
public partial class PageLink : ObservableObject {
    /// <summary>A clickable page number (or a Prev/Next arrow that targets an adjacent page).</summary>
    public PageLink(string label, int pageNumber, bool isCurrent, bool isEnabled, Action<int> onSelected) {
        Label = label;
        PageNumber = pageNumber;
        IsCurrent = isCurrent;
        IsEnabled = isEnabled;
        SelectCommand = new RelayCommand(() => onSelected(pageNumber), () => isEnabled && !isCurrent);
    }

    private PageLink(string label) {
        // Ellipsis gap: no target page, never clickable.
        Label = label;
        PageNumber = null;
        IsCurrent = false;
        IsEnabled = false;
        SelectCommand = new RelayCommand(() => { }, () => false);
    }

    /// <summary>Builds a non-clickable ellipsis gap ("…").</summary>
    public static PageLink Ellipsis() => new("…");

    public string Label { get; }

    /// <summary>The page this item navigates to, or null for an ellipsis gap.</summary>
    public int? PageNumber { get; }

    /// <summary>True for the page the user is currently on (highlighted, not clickable).</summary>
    public bool IsCurrent { get; }

    /// <summary>False for the ellipsis gap and for Prev/Next at the ends of the range.</summary>
    public bool IsEnabled { get; }

    public ICommand SelectCommand { get; }
}
