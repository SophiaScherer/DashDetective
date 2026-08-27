using System;
using System.Collections.Generic;

namespace DashDetective.Shared;

/// <summary>A page whose widgets the user can reorder, and whose order is persisted. The shell reads
/// and writes it; the page itself only holds it.</summary>
public interface IReorderablePage {
    /// <summary>Key this page's order is saved under.</summary>
    string PageKey { get; }

    /// <summary>The widget ids in display order. Empty until the board reports one.</summary>
    IReadOnlyList<string> WidgetOrder { get; set; }

    /// <summary>Raised when a drag changes the order, so the shell can persist it.</summary>
    event Action? WidgetOrderChanged;
}
