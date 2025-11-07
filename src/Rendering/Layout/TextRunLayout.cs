using System;
using System.Collections.Generic;
using SkiaMarkdown.Core.Syntax;
using SkiaSharp;

namespace SkiaMarkdown.Rendering.Layout;

/// <summary>
/// Represents a laid out text run.
/// </summary>
public sealed class TextRunLayout : LayoutElement
{
    public TextRunLayout(
        string text,
        SKRect bounds,
        float baseline,
        float fontSize,
        SKTypeface? typeface = null,
        SKColor? color = null,
        MarkdownInline? source = null,
        string? linkDestination = null,
        float[]? glyphOffsets = null)
        : base(bounds, source: source, textContent: text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Baseline = baseline;
        FontSize = fontSize;
        Typeface = typeface;
        Color = color ?? SKColors.Black;
        LinkDestination = linkDestination;
        GlyphOffsets = glyphOffsets ?? Array.Empty<float>();
    }

    public string Text { get; }

    public float Baseline { get; }

    public float FontSize { get; }

    public SKTypeface? Typeface { get; }

    public SKColor Color { get; }

    public string? LinkDestination { get; }

    /// <summary>
    /// Gets cumulative glyph offsets in device units for each character, starting at zero.
    /// </summary>
    public IReadOnlyList<float> GlyphOffsets { get; }

    public int GetCharacterIndexAt(float x)
    {
        if (GlyphOffsets.Count == 0)
        {
            return 0;
        }

        var positions = GlyphOffsets;
        var max = positions[^1];
        if (x <= 0)
        {
            return 0;
        }

        if (x >= max)
        {
            return positions.Count - 1;
        }

        for (var i = 1; i < positions.Count; i++)
        {
            if (x < positions[i])
            {
                return i - 1;
            }
        }

        return positions.Count - 1;
    }
}
