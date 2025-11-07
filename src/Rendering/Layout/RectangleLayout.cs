using SkiaSharp;

namespace SkiaMarkdown.Rendering.Layout;

/// <summary>
/// Represents a filled rectangle (optionally with stroke).
/// </summary>
public sealed class RectangleLayout : LayoutElement
{
    public RectangleLayout(SKRect bounds, SKColor fillColor, SKColor? strokeColor = null, float strokeThickness = 0, float cornerRadius = 0)
        : base(bounds)
    {
        FillColor = fillColor;
        StrokeColor = strokeColor;
        StrokeThickness = strokeThickness;
        CornerRadius = cornerRadius;
    }

    public SKColor FillColor { get; }

    public SKColor? StrokeColor { get; }

    public float StrokeThickness { get; }

    public float CornerRadius { get; }
}
