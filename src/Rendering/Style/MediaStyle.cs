using SkiaSharp;

namespace SkiaMarkdown.Rendering.Style;

/// <summary>
/// Describes theming for media placeholders and captions.
/// </summary>
public sealed record MediaStyle(
    float MaxImageWidth,
    float DefaultImageHeight,
    float DefaultVideoHeight,
    SKColor PlaceholderBackground,
    SKColor PlaceholderBorder,
    TextStyle PlaceholderText,
    float CornerRadius,
    float Spacing);
