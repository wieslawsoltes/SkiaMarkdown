using System;
using System.Collections.Generic;
using SkiaSharp;

namespace SkiaMarkdown.Rendering;

/// <summary>
/// Caches reusable drawing resources to minimise per-frame allocations.
/// </summary>
internal sealed class RenderCache : IDisposable
{
    public SKTextBlob? GetOrAddTextBlob(string text, float fontSize, SKTypeface typeface)
        => null;

    public void Clear()
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
