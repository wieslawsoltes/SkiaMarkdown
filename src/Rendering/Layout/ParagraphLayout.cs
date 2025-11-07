using SkiaMarkdown.Core.Syntax;
using SkiaSharp;

namespace SkiaMarkdown.Rendering.Layout;

/// <summary>
/// Represents a block paragraph with inline children.
/// </summary>
public sealed class ParagraphLayout : LayoutElement
{
    public ParagraphLayout(SKRect bounds, IEnumerable<LayoutElement> inlines, MarkdownBlock? source = null, string? textContent = null)
        : base(bounds, inlines, source, textContent)
    {
    }
}
