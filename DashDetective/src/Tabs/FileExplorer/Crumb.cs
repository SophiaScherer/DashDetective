using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// One segment of the breadcrumb bar. Rebuilt whenever the current folder changes; clicking a
/// crumb navigates to that ancestor. <see cref="Separator"/> is the "›" shown after all but the
/// last crumb, and <see cref="IsCurrent"/> marks the leaf (styled as the active location).
/// </summary>
public class Crumb {
    public Crumb(string label, string fullPath, string separator, bool isCurrent,
                 Action<Crumb> onSelected) {
        Label = label;
        FullPath = fullPath;
        Separator = separator;
        IsCurrent = isCurrent;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }
    public string FullPath { get; }
    public string Separator { get; }
    public bool IsCurrent { get; }
    public ICommand SelectCommand { get; }
}
