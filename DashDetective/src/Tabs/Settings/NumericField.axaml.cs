using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Globalization;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// A small whole-number entry with its unit beside it — "90 %", "10 s". Replaces the segmented runs the
/// Alerts card used to carry: five or six presets per row pushed the description text into a narrow
/// column, and a threshold is a number people want to type rather than pick from a shortlist.
///
/// Typing is filtered to digits, so the box cannot hold something that is not a number, and the value is
/// clamped into <see cref="Minimum"/>..<see cref="Maximum"/> when the edit is committed (focus leaves, or
/// Enter). An emptied or unusable box reverts to the last good value rather than reporting zero — zero is
/// a meaningful threshold to the settings layer, so guessing it here would silently change what is
/// watched. Escape abandons the edit.
///
/// Feature-local rather than in <c>src/Shared/Controls</c>: Settings is the only page with a number to
/// type. Promote it if a second one appears.
/// </summary>
public partial class NumericField : UserControl {
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<NumericField, int>(
            nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> MinimumProperty =
        AvaloniaProperty.Register<NumericField, int>(nameof(Minimum));

    public static readonly StyledProperty<int> MaximumProperty =
        AvaloniaProperty.Register<NumericField, int>(nameof(Maximum), defaultValue: int.MaxValue);

    public static readonly StyledProperty<string> SuffixProperty =
        AvaloniaProperty.Register<NumericField, string>(nameof(Suffix), defaultValue: "");

    /// <summary>The number shown, and what a committed edit writes back. Two-way by default.</summary>
    public int Value {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>The lowest value a committed edit may produce.</summary>
    public int Minimum {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>The highest value a committed edit may produce.</summary>
    public int Maximum {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>The unit shown beside the box, e.g. "%" or "s".</summary>
    public string Suffix {
        get => GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    public NumericField() {
        InitializeComponent();

        Entry.AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        Entry.TextChanged += (_, _) => Capture();
        Entry.LostFocus += (_, _) => Render();
        Entry.KeyDown += OnKeyDown;

        Render();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
            Render();
        else if (change.Property == SuffixProperty)
            SuffixLabel.Text = Suffix;
    }

    /// <summary>Digits only. Tunnelled so the character is refused before the box ever holds it — pasted
    /// text arrives here too, so there is no second path that can put a letter in the box.</summary>
    private void OnTextInput(object? sender, TextInputEventArgs e) {
        if (e.Text is not { } text)
            return;

        foreach (var character in text)
            if (!char.IsAsciiDigit(character)) {
                e.Handled = true;
                return;
            }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key == Key.Enter) {
            Render();
            e.Handled = true;
            return;
        }

        // Escape abandons the edit and puts the committed value back, so a half-typed number can be
        // backed out of without having to remember what was there.
        if (e.Key == Key.Escape) {
            Render();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Takes the typed number as it is typed, so a value can never be lost. Committing on focus loss
    /// alone was not enough: clicking anything that does not take focus — a card, a heading, the page
    /// background — leaves the box focused, so a number typed and then left alone was never stored.
    ///
    /// Deliberately does NOT rewrite the box. Clamping mid-edit would fight the typing (in a 1..99 field,
    /// the "10" on the way to "100" is not what anyone meant to stop at), so what is shown stays the
    /// user's text and <see cref="Render"/> reconciles it when the edit ends. An empty or unparseable box
    /// leaves the stored value alone rather than resolving to zero: zero is a meaningful threshold to the
    /// settings layer, so inventing one here would silently change what is watched.
    /// </summary>
    private void Capture() {
        if (_rendering)
            return;

        // Only the ceiling is enforced mid-edit. Raising a too-small number to the minimum would rewrite
        // the box under the caret — the "0" of an "05" becomes "1", and then typing the 5 gives 15 — and
        // a half-typed number below the floor is on its way somewhere, not a value anyone chose. The
        // floor is applied when the edit ends instead.
        if (int.TryParse(Entry.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var typed) &&
            typed >= Minimum)
            Value = Math.Min(typed, Maximum);
    }

    /// <summary>Puts the stored value back in the box, which is where a clamped or abandoned edit is
    /// reconciled: type 999 into a 1..100 field and the box reads 100 once the edit ends.</summary>
    private void Render() {
        var text = Value.ToString(CultureInfo.InvariantCulture);
        if (Entry.Text == text)
            return;

        _rendering = true;
        Entry.Text = text;
        Entry.CaretIndex = text.Length;
        _rendering = false;
    }

    // Set while Render writes to the box, so its own TextChanged is not read back as a user edit.
    private bool _rendering;
}
