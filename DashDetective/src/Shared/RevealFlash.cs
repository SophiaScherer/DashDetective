using Avalonia;
using Avalonia.Threading;
using System;

namespace DashDetective.Shared;

/// <summary>
/// Tints an element for a moment when something elsewhere jumps to it (search landing on a setting,
/// Performance naming a drive). Only toggles the class; the fade is <c>Border.revealFlash</c>'s
/// transition.
/// </summary>
public static class RevealFlash {
    /// <summary>How long the tint holds before fading back.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(1.6);

    /// <summary>Re-adds the class so a second reveal restarts the hold.</summary>
    public static void Flash(StyledElement target) {
        target.Classes.Remove("highlighted");
        target.Classes.Add("highlighted");
        DispatcherTimer.RunOnce(() => target.Classes.Remove("highlighted"), Duration);
    }
}
