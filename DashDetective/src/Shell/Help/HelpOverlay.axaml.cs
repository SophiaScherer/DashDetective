using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DashDetective.Shared;
using System;
using System.ComponentModel;
using System.Linq;

namespace DashDetective.Shell.Help;

/// <summary>
/// The Help modal: a full-window scrim with a centred card. Embedded directly by the shell (like the
/// navigation bar) rather than routed through the <c>ViewLocator</c>, so it can sit above every other
/// surface including the nav bar.
///
/// Only view concerns live here: the scrim-press dismissal, moving focus in and out, and the reveal
/// flash. Esc is not handled here: the shell's shortcut dispatcher owns the key for the whole app and
/// closes this modal ahead of anything else, so there is one place that decides what Esc means.
/// </summary>
public partial class HelpOverlay : UserControl {
    private HelpViewModel? _boundViewModel;

    // What held focus before Help took it, and whether it showed a focus ring, so closing can hand it back.
    private IInputElement? _focusBeforeOpen;
    private bool _focusBeforeOpenWasVisible;

    public HelpOverlay() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null) {
            _boundViewModel.RevealRequested -= OnRevealRequested;
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _boundViewModel = DataContext as HelpViewModel;

        if (_boundViewModel is not null) {
            _boundViewModel.RevealRequested += OnRevealRequested;
            _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    /// <summary>Whether a press landed on the card rather than the scrim. The card itself counts:
    /// <c>IsVisualAncestorOf</c> is false for the element, and an empty spot on the card reports the card.</summary>
    internal static bool IsInsideCard(Visual card, object? source) =>
        source is Visual visual && (visual == card || card.IsVisualAncestorOf(visual));

    // A press anywhere on the scrim closes, except one that landed inside the card — otherwise
    // interacting with the modal's own content would dismiss it.
    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (IsInsideCard(Card, e.Source))
            return;

        _boundViewModel?.Close();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName != nameof(HelpViewModel.IsOpen) || _boundViewModel is null)
            return;

        if (_boundViewModel.IsOpen)
            TakeFocus();
        else
            ReturnFocus();
    }

    /// <summary>Moves focus onto the card, so Tab starts inside it and nothing behind the scrim can be
    /// reached. Posted because the card is not visible until this layout pass has run.</summary>
    private void TakeFocus() {
        _focusBeforeOpen = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        _focusBeforeOpenWasVisible = _focusBeforeOpen is StyledElement { Classes: var classes } &&
                                     classes.Contains(":focus-visible");

        // Ringed when focus arrived from the keyboard, so a keyboard user can see what Enter will press.
        var method = _focusBeforeOpenWasVisible ? NavigationMethod.Tab : NavigationMethod.Pointer;
        Dispatcher.UIThread.Post(() => {
            if (_boundViewModel is { IsOpen: true })
                CloseButton.Focus(method);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Hands focus back to what held it before Help opened, ring included, if that is still on
    /// screen; otherwise clears it rather than leave it on a hidden button. Posted so the modal has
    /// hidden first.</summary>
    private void ReturnFocus() {
        var previous = _focusBeforeOpen;
        var ring = _focusBeforeOpenWasVisible;
        _focusBeforeOpen = null;

        Dispatcher.UIThread.Post(() => {
            var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
            var current = focusManager?.GetFocusedElement();

            // Focus already went somewhere outside the modal on purpose; leave it there.
            if (current is Visual visual && !this.IsVisualAncestorOf(visual))
                return;

            if (previous is InputElement { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Focusable: true } target &&
                target.IsAttachedToVisualTree())
                target.Focus(RestoreMethod(target, ring));
            else if (current is not null)
                focusManager?.Focus(null);
        });
    }

    /// <summary>How to hand focus back. A text box is never given the keyboard route: focusing one that
    /// way selects all its text, so the next key would wipe what the user was typing before F1.</summary>
    internal static NavigationMethod RestoreMethod(IInputElement target, bool ring) =>
        ring && target is not TextBox ? NavigationMethod.Tab : NavigationMethod.Pointer;

    /// <summary>
    /// Scrolls a topic into view and flashes it. Rows are found by the key in their <c>Tag</c> rather
    /// than by name, so a topic added to the content table becomes reachable without touching this file.
    ///
    /// Posted because the reveal arrives in the same breath as the open and the tab switch that made the
    /// row's section visible: it is not in the visual tree until that layout pass has run.
    /// </summary>
    private void OnRevealRequested(string topicKey) =>
        Dispatcher.UIThread.Post(() => {
            if (FindRow(topicKey) is not { } row)
                return;

            row.BringIntoView();
            RevealFlash.Flash(row);
        }, DispatcherPriority.Loaded);

    private Border? FindRow(string topicKey) =>
        this.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Tag as string == topicKey);
}
