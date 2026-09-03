namespace DashDetective.Services.Links;

/// <summary>
/// Hands a web address to the default browser. Never throws: a refused address or a failed launch
/// reports <c>false</c>.
///
/// No platform arms — <c>UseShellExecute</c> reaches the shell on Windows and <c>xdg-open</c> on Linux,
/// so this is portable managed code and keeps its plain name.
/// </summary>
internal interface IWebLinkOpener {
    /// <summary>Opens <paramref name="url"/>, reporting whether the launch started. Anything that is not
    /// <c>https://</c> is refused.</summary>
    bool Open(string url);
}
