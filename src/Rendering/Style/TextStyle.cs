using SkiaSharp;

namespace SkiaMarkdown.Rendering.Style;

/// <summary>
/// Describes typography and colour for a text run.
/// </summary>
public sealed record TextStyle(SKTypeface Typeface, float FontSize, SKColor Color)
{
    public TextStyle WithColor(SKColor color) => this with { Color = color };

    public TextStyle WithFontSize(float size) => this with { FontSize = size };

    public TextStyle WithTypeface(SKTypeface typeface) => this with { Typeface = typeface };
}
