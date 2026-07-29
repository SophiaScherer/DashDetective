using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DashDetective.Shared.Completion;
using System.Windows.Input;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A text box that ghosts the rest of a suggestion after the caret, which Tab fills in — the folder
/// path bar, the process filter and the toolbar search all use it.
///
/// Tab is handled here rather than through <c>ShortcutCatalog</c> on purpose: whether there is a
/// suggestion to accept is a property of the focused field, and only the field knows. Handling it
/// locally also keeps Tab doing its normal job everywhere else, since the key is marked handled only
/// when a suggestion was actually taken.
/// </summary>
public partial class GhostCompletionBox : UserControl {
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<GhostCompletionBox, string>(
            nameof(Text), defaultValue: "", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The full string the field should complete to, including what is already typed. Null or
    /// a value that doesn't extend <see cref="Text"/> draws no ghost.</summary>
    public static readonly StyledProperty<string?> CompletionProperty =
        AvaloniaProperty.Register<GhostCompletionBox, string?>(nameof(Completion));

    /// <summary>Placeholder shown while the box is empty.</summary>
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<GhostCompletionBox, string?>(nameof(PlaceholderText));

    /// <summary>Run when Enter is pressed in the box. Bound here rather than through the shell because
    /// the shortcut layer deliberately leaves Enter to whatever text box has focus.</summary>
    public static readonly StyledProperty<ICommand?> EnterCommandProperty =
        AvaloniaProperty.Register<GhostCompletionBox, ICommand?>(nameof(EnterCommand));

    public GhostCompletionBox() {
        InitializeComponent();

        Entry.KeyDown += OnEntryKeyDown;
        Entry.GetObservable(TextBox.TextProperty).Subscribe(new AnonymousObserver(OnEntryTextChanged));
    }

    public string Text {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Completion {
        get => GetValue(CompletionProperty);
        set => SetValue(CompletionProperty, value);
    }

    public string? PlaceholderText {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public ICommand? EnterCommand {
        get => GetValue(EnterCommandProperty);
        set => SetValue(EnterCommandProperty, value);
    }

    /// <summary>Puts the caret in the box and selects what is there, so the next keystroke replaces the
    /// term rather than appending to it — what every find bar does. Posted because the focus request
    /// often arrives in the same breath as the binding that reveals the box.</summary>
    public void FocusAndSelectAll() =>
        Dispatcher.UIThread.Post(() => {
            Entry.Focus();
            Entry.SelectAll();
        }, DispatcherPriority.Input);

    /// <summary>Fills in the ghosted suggestion and puts the caret at the end. Returns whether there was
    /// one to take.</summary>
    public bool TryAcceptCompletion() {
        if (Completion is not { } completion || PrefixCompleter.Suffix(Text, completion).Length == 0)
            return false;

        Text = completion;
        Entry.Text = completion;
        Entry.CaretIndex = completion.Length;
        return true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty) {
            // Guarded: the box also writes back to Text, and assigning the same string again would
            // reset the caret to the start mid-word.
            if (Entry.Text != Text)
                Entry.Text = Text;
            UpdateGhost();
        } else if (change.Property == CompletionProperty) {
            UpdateGhost();
        } else if (change.Property == PlaceholderTextProperty) {
            Entry.PlaceholderText = PlaceholderText;
        } else if (change.Property == EnterCommandProperty) {
            SetEnterBinding();
        } else if (change.Property == FontSizeProperty) {
            Entry.FontSize = FontSize;
            Ghost.FontSize = FontSize;
        }
    }

    private void OnEntryTextChanged(string? text) {
        Text = text ?? "";
        UpdateGhost();
    }

    private void OnEntryKeyDown(object? sender, KeyEventArgs e) {
        // Only a Tab that actually took a suggestion is consumed; otherwise it falls through and moves
        // focus, which is what Tab does in a form.
        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None && TryAcceptCompletion())
            e.Handled = true;
    }

    // The ghost repeats the typed text in Transparent so the visible suffix begins exactly at the caret.
    private void UpdateGhost() {
        var suffix = PrefixCompleter.Suffix(Text, Completion);

        Ghost.Inlines?.Clear();
        if (suffix.Length == 0)
            return;

        Ghost.Inlines?.Add(new Run(Text) { Foreground = Brushes.Transparent });
        Ghost.Inlines?.Add(new Run(suffix) { Foreground = GhostBrush() });
    }

    private IBrush? GhostBrush() =>
        this.FindResource("TextFaint") as IBrush ?? Foreground;

    private void SetEnterBinding() {
        Entry.KeyBindings.Clear();
        if (EnterCommand is { } command)
            Entry.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Enter), Command = command });
    }

    /// <summary>Minimal observer so the box's own edits reach <see cref="Text"/>; Avalonia's property
    /// observables have no plain callback overload.</summary>
    private sealed class AnonymousObserver : System.IObserver<string?> {
        private readonly System.Action<string?> _onNext;

        public AnonymousObserver(System.Action<string?> onNext) => _onNext = onNext;

        public void OnCompleted() { }
        public void OnError(System.Exception error) { }
        public void OnNext(string? value) => _onNext(value);
    }
}
