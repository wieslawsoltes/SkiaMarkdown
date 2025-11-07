using System;

namespace SkiaMarkdown.Avalonia.Controls;

/// <summary>
/// Provides data for link interaction events within the Markdown view.
/// </summary>
public sealed class MarkdownLinkEventArgs : EventArgs
{
    public MarkdownLinkEventArgs(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            throw new ArgumentException("Link cannot be null or whitespace.", nameof(link));
        }

        Link = link;
    }

    /// <summary>
    /// Gets the resolved link destination.
    /// </summary>
    public string Link { get; }
}
