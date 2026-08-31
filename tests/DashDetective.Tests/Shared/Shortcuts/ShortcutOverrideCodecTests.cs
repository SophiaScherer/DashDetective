using Avalonia.Input;
using DashDetective.Shared.Shortcuts;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared.Shortcuts;

/// <summary>Covers <see cref="ShortcutOverrideCodec"/>: the round trip, that names rather than ordinals
/// are stored, and that a malformed or stale entry is skipped rather than fatal — a settings file is
/// hand-editable and outlives the enum members it names.</summary>
public class ShortcutOverrideCodecTests {
    // The codec's own separators, by code point exactly as the codec declares them: a raw control
    // character in a source file is invisible in review and does not survive a careless edit.
    private const char Entry = (char)0x1E;
    private const char Field = (char)0x1F;

    private static Dictionary<ShortcutId, KeyGesture> Overrides(params (ShortcutId, KeyGesture)[] entries) {
        var map = new Dictionary<ShortcutId, KeyGesture>();
        foreach (var (id, gesture) in entries)
            map[id] = gesture;
        return map;
    }

    [Fact]
    public void RoundTrips() {
        var original = Overrides(
            (ShortcutId.Export, new KeyGesture(Key.G, KeyModifiers.Control)),
            (ShortcutId.EndTask, new KeyGesture(Key.K, KeyModifiers.Control | KeyModifiers.Shift)),
            (ShortcutId.Escape, new KeyGesture(Key.F12)));

        var decoded = ShortcutOverrideCodec.Decode(ShortcutOverrideCodec.Encode(original));

        Assert.Equal(3, decoded.Count);
        foreach (var (id, gesture) in original) {
            Assert.Equal(gesture.Key, decoded[id].Key);
            Assert.Equal(gesture.KeyModifiers, decoded[id].KeyModifiers);
        }
    }

    /// <summary>Ordinals would silently re-point someone's keyboard when an enum member is inserted.</summary>
    [Fact]
    public void StoresNamesNotOrdinals() {
        var encoded = ShortcutOverrideCodec.Encode(
            Overrides((ShortcutId.Export, new KeyGesture(Key.G, KeyModifiers.Control))));

        Assert.Equal($"Export{Field}G{Field}Control", encoded);
    }

    [Fact]
    public void Encode_NothingOverridden_IsEmpty() =>
        Assert.Equal("", ShortcutOverrideCodec.Encode(Overrides()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("Export")]
    public void Decode_UnusableInput_YieldsNoOverrides(string? encoded) =>
        Assert.Empty(ShortcutOverrideCodec.Decode(encoded));

    /// <summary>A binding for an action a later version removed is dropped, leaving that shortcut on its
    /// default — the rest of the file still has to load.</summary>
    [Fact]
    public void Decode_SkipsAnUnknownEntry_AndKeepsTheRest() {
        var good = ShortcutOverrideCodec.Encode(
            Overrides((ShortcutId.Export, new KeyGesture(Key.G, KeyModifiers.Control))));

        var decoded = ShortcutOverrideCodec.Decode(
            $"NoSuchAction{Field}G{Field}Control{Entry}{good}");

        Assert.Single(decoded);
        Assert.Equal(Key.G, decoded[ShortcutId.Export].Key);
    }

    [Fact]
    public void Decode_SkipsAnUnknownKeyOrModifier() {
        Assert.Empty(ShortcutOverrideCodec.Decode($"Export{Field}NoSuchKey{Field}Control"));
        Assert.Empty(ShortcutOverrideCodec.Decode($"Export{Field}G{Field}NoSuchModifier"));
    }

    /// <summary>A binding of "Ctrl" alone would fire the moment the key was touched. The capture control
    /// refuses to produce one; this refuses to load one a hand-edit put there.</summary>
    [Theory]
    [InlineData("LeftCtrl")]
    [InlineData("RightShift")]
    [InlineData("LeftAlt")]
    [InlineData("None")]
    public void Decode_RefusesAModifierOnlyBinding(string key) =>
        Assert.Empty(ShortcutOverrideCodec.Decode($"Export{Field}{key}{Field}None"));

    [Fact]
    public void Decode_SkipsAnEntryWithTheWrongFieldCount() =>
        Assert.Empty(ShortcutOverrideCodec.Decode($"Export{Field}G"));

    /// <summary>What the settings round trip actually stores: several entries in one string.</summary>
    [Fact]
    public void Encode_SeparatesEntries() {
        var encoded = ShortcutOverrideCodec.Encode(Overrides(
            (ShortcutId.Export, new KeyGesture(Key.G, KeyModifiers.Control)),
            (ShortcutId.ToggleLive, new KeyGesture(Key.J, KeyModifiers.Control))));

        Assert.Equal(2, encoded.Split(Entry, StringSplitOptions.None).Length);
    }
}
