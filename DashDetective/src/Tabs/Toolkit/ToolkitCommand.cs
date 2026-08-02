namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// A command the user authored, exactly as they typed it. This — not the <see cref="ToolkitEntry"/>
/// derived from it — is what <see cref="ToolkitCommandCodec"/> persists, so the edit form can be
/// re-filled with the user's own words rather than with something reconstructed from a
/// <see cref="ToolkitAction"/>.
/// </summary>
/// <param name="Title">The row's primary label, and its identity for pins and search reveal — which is
/// why <see cref="ToolkitCommandValidator"/> refuses a duplicate.</param>
/// <param name="Description">The one-line subtitle. Optional; a blank one simply reads as a bare row.</param>
/// <param name="Type">What running it does.</param>
/// <param name="Payload">The path, program or URL the type calls for.</param>
/// <param name="Arguments">The arguments as one typed string, split by
/// <see cref="ToolkitArgumentParser"/> on the way to the OS. Kept raw so a round-trip through settings
/// gives the form back what was typed. Meaningless for a folder or a URL.</param>
/// <param name="Category">The section the user also wants it filed under, or <c>null</c> for the Custom
/// section alone.</param>
public sealed record ToolkitCommand(
    string Title,
    string Description,
    ToolkitCommandType Type,
    string Payload,
    string Arguments = "",
    ToolkitCategory? Category = null);
