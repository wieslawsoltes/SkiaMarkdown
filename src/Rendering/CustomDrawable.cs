using System;
using SkiaSharp;

namespace SkiaMarkdown.Rendering;

/// <summary>
/// Represents a user-provided drawing routine for custom placeholders.
/// </summary>
public sealed class CustomDrawable
{
    private readonly Action<SKCanvas, SKRect> _draw;

    public CustomDrawable(Action<SKCanvas, SKRect> draw)
    {
        _draw = draw ?? throw new ArgumentNullException(nameof(draw));
    }

    public void Draw(SKCanvas canvas, SKRect bounds)
    {
        if (canvas is null)
        {
            throw new ArgumentNullException(nameof(canvas));
        }

        _draw(canvas, bounds);
    }
}
