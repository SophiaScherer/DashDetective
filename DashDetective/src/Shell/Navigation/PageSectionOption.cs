using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Shell.Navigation;

/// <summary>One row of the nav bar's section list: the section plus the command that jumps to it.</summary>
public sealed class PageSectionOption {
    public PageSectionOption(PageSection section, Action<PageSection> onPicked) {
        Section = section;
        SelectCommand = new RelayCommand(() => onPicked(section));
    }

    public PageSection Section { get; }
    public string Title => Section.Title;
    public ICommand SelectCommand { get; }
}
