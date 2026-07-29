using System;

namespace DashDetective.Services.Search;

/// <summary>
/// One file or folder a search turned up. Deliberately narrower than the File Explorer's
/// <c>FileItem</c>: a search result only needs enough to draw a row and navigate to it, and the two
/// sources that produce these (the Windows index and the fallback scan) can both supply this much
/// cheaply — asking either for size or type strings would cost a stat call per hit.
/// </summary>
/// <param name="Name">The file or folder name, as shown on the result row.</param>
/// <param name="FullPath">The full path, used to reveal the item in the File Explorer.</param>
/// <param name="FolderPath">The containing folder, shown as the row's subtitle.</param>
/// <param name="IsDirectory">Whether the hit is a folder (navigated into rather than selected).</param>
/// <param name="Modified">Last write time; the index orders by it so recent work surfaces first.</param>
public readonly record struct FileHit(
    string Name,
    string FullPath,
    string FolderPath,
    bool IsDirectory,
    DateTime Modified);
