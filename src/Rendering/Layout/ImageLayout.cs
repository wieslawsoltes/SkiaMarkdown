using SkiaSharp;

namespace SkiaMarkdown.Rendering.Layout;

/// <summary>
/// Represents an image to be rendered from an external source.
/// </summary>
public sealed class ImageLayout : LayoutElement
{
    public ImageLayout(SKRect bounds, string source, string? alternativeText)
        : base(bounds)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        AlternativeText = alternativeText;
    }

    public string Source { get; }

    public string? AlternativeText { get; }
}
