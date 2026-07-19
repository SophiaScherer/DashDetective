using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Globalization;
using System.Windows.Input;

namespace DashDetective.Tabs.Network;

/// <summary>
/// One numbered page in the Active Connections pager (a plain "1, 2, 3, …" system). Same
/// selectable-item-VM shape as <c>FilterOption</c>: immutable display state plus a
/// <see cref="SelectCommand"/> that calls back into the owning VM with the target page. The current
/// page is highlighted (<see cref="IsCurrent"/>) and its command is a no-op, so clicking it does
/// nothing. There is no ellipsis or Prev/Next chrome — the connections list is capped at ten pages,
/// so every number fits on one row.
/// </summary>
public partial class PageLink : ObservableObject {
    public PageLink(int pageNumber, bool isCurrent, Action<int> onSelected) {
        PageNumber = pageNumber;
        Label = pageNumber.ToString(CultureInfo.InvariantCulture);
        IsCurrent = isCurrent;
        SelectCommand = new RelayCommand(() => onSelected(pageNumber), () => !isCurrent);
    }

    public string Label { get; }
    public int PageNumber { get; }

    /// <summary>True for the page the user is currently on (highlighted, not clickable).</summary>
    public bool IsCurrent { get; }

    public ICommand SelectCommand { get; }
}
