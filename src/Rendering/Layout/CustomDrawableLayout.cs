using System.Collections.Generic;
using SkiaSharp;

namespace SkiaMarkdown.Rendering.Layout;

/// <summary>
/// Represents a placeholder that can be fulfilled by a custom drawable.
/// </summary>
public sealed class CustomDrawableLayout : LayoutElement
{
    public CustomDrawableLayout(SKRect bounds, string identifier, IReadOnlyDictionary<string, string> metadata)
        : base(bounds)
    {
        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public string Identifier { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}
