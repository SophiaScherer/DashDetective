using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// A jump from a Performance resource to the tab that owns it — what the link says, and what it does.
/// Built by <see cref="PerformanceViewModel"/>, which alone holds the device's identity; the row that
/// carries it stays a data model and learns nothing about other tabs.
/// </summary>
public sealed class ResourceLink {
    public ResourceLink(string label, Action activate) {
        Label = label;
        Command = new RelayCommand(activate);
    }

    /// <summary>What the link button reads, e.g. "View in Storage".</summary>
    public string Label { get; }

    /// <summary>Runs the jump. Wrapped once at construction, not minted per binding read.</summary>
    public ICommand Command { get; }
}
