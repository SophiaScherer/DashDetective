using Avalonia;
using Avalonia.Threading;
using System;

namespace DashDetective.Shared;

/// <summary>
/// Tints an element for a moment when something elsewhere in the app jumps to it — universal search
/// landing on a setting, or the Performance tab naming a drive or adapter.
///
/// Only the class is toggled here; the fade back is the shared <c>Border.revealFlash</c> style's brush
/// transition, so a caller that forgets the class gets an instant blink rather than a broken flash.
/// Four tabs had each written this same three-line pair with their own copy of the duration.
/// </summary>
public static class RevealFlash {
    /// <summary>How long the tint holds before fading back.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(1.6);

    /// <summary>Re-adds the class from scratch so a second reveal of the same row restarts the hold
    /// rather than inheriting the first one's remaining time.</summary>
    public static void Flash(StyledElement target) {
        target.Classes.Remove("highlighted");
        target.Classes.Add("highlighted");
        DispatcherTimer.RunOnce(() => target.Classes.Remove("highlighted"), Duration);
    }
}
