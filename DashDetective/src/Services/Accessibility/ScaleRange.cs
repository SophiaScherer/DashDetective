using System;

namespace DashDetective.Services.Accessibility;

/// <summary>
/// The one range both scales offer — 80% to 200% in 5% steps — and the arithmetic over it. Pure, so it
/// is testable without a layout pass, and shared so the interface size and the text size cannot drift
/// into offering different things.
/// </summary>
internal static class ScaleRange {
    internal const int MinPercent = 80;

    internal const int MaxPercent = 200;

    internal const int StepPercent = 5;

    /// <summary>The size the app ships at, and the one every option on the card must leave untouched.</summary>
    internal const int DefaultPercent = 100;

    /// <summary>A stored value pulled into range and onto a step, so a slider and a % field can both
    /// show it exactly — which is what replaced snapping onto a handful of offered sizes. The clamp is
    /// load-bearing: settings.json is hand-editable, and 0 would collapse the window rather than
    /// degrade.</summary>
    internal static int Normalize(int percent) {
        // Integer rounding: an exact midpoint cannot arise, since a 5% step never halves onto a whole
        // percent.
        var clamped = Math.Clamp(percent, MinPercent, MaxPercent);
        return (clamped + (StepPercent / 2)) / StepPercent * StepPercent;
    }

    /// <summary>A percentage as a multiplier. Normalized first, so the factor in force and the number on
    /// screen can never disagree.</summary>
    internal static double Factor(int percent) => Normalize(percent) / 100.0;
}
