using SkiaSharp;

namespace SkiaMarkdown.Rendering.Style;

public sealed record CodeBlockStyle(
    TextStyle Text,
    SKColor BackgroundColor,
    SKColor BorderColor,
    float Padding,
    float CornerRadius,
    float Spacing);
