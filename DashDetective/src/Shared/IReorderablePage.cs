using System.Collections.Generic;

namespace DashDetective.Shared;

/// <summary>A page whose widgets the user can reorder. The shell reads and writes the orders; the page
/// itself only holds them.</summary>
public interface IReorderablePage {
    /// <summary>Every order this page persists — one per reorderable strip.</summary>
    IEnumerable<SavedOrder> SavedOrders { get; }
}
