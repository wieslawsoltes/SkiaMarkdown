using SkiaSharp;

namespace SkiaMarkdown.Rendering.Layout;

/// <summary>
/// Represents a horizontal divider.
/// </summary>
public sealed class DividerLayout : LayoutElement
{
    public DividerLayout(SKRect bounds, float thickness, SKColor color)
        : base(bounds)
    {
        Thickness = thickness;
        Color = color;
    }

    public float Thickness { get; }

    public SKColor Color { get; }
}
