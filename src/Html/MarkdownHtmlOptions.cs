namespace SkiaMarkdown.Html;

using System;
using System.Collections.Generic;

/// <summary>
/// Options that influence Markdown-to-HTML generation.
/// </summary>
public sealed record MarkdownHtmlOptions
{
    /// <summary>
    /// Gets a shared options instance with the default configuration.
    /// </summary>
    public static MarkdownHtmlOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether raw HTML found in the source should be emitted as-is.
    /// </summary>
    public bool AllowRawHtml { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the GitHub tag filter should be applied even when raw HTML is allowed.
    /// </summary>
    public bool FilterDisallowedRawHtml { get; init; } = true;

    /// <summary>
    /// Gets or sets the collection of tag names that should be filtered when <see cref="FilterDisallowedRawHtml"/> is enabled.
    /// When <c>null</c>, the default GFM list is used.
    /// </summary>
    public IReadOnlyCollection<string>? DisallowedRawHtmlTags { get; init; }

    /// <summary>
    /// Gets or sets the string used for soft line breaks (default: newline).
    /// </summary>
    public string SoftBreak { get; init; } = "\n";

    /// <summary>
    /// Gets or sets a delegate that resolves emoji shortcodes (without surrounding colons)
    /// to glyphs. Return <c>null</c> or empty to fall back to the original shortcode text.
    /// </summary>
    public Func<string, string?>? EmojiResolver { get; init; }
}
