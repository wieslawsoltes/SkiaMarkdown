using System.Collections.Generic;
using SkiaMarkdown.Core.Syntax;
using SkiaSharp;

namespace SkiaMarkdown.Rendering.Layout;

/// <summary>
/// Represents a layout element with bounds and children.
/// </summary>
public abstract class LayoutElement
{
    protected LayoutElement(SKRect bounds, IEnumerable<LayoutElement>? children = null, MarkdownNode? source = null, string? textContent = null)
    {
        Bounds = bounds;
        Children = children is null ? Array.Empty<LayoutElement>() : new List<LayoutElement>(children);
        Source = source;
        TextContent = textContent;
    }

    public SKRect Bounds { get; protected set; }

    public IList<LayoutElement> Children { get; }

    public MarkdownNode? Source { get; }

    public string? TextContent { get; }
}
