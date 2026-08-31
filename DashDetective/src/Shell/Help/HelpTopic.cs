namespace DashDetective.Shell.Help;

/// <summary>One entry of Help copy: a bullet under Tips, or a page under Getting started.</summary>
/// <param name="Key">Stable slug for this entry, used as its search identity and reveal target.</param>
/// <param name="Title">The heading, e.g. a page name. Null for a tip, which is body text alone.</param>
/// <param name="Body">The sentence or two shown to the reader.</param>
public sealed record HelpTopic(string Key, string? Title, string Body);
