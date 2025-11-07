using SkiaSharp;

namespace SkiaMarkdown.Rendering.Style;

public sealed record BlockquoteStyle(
    TextStyle Text,
    SKColor BarColor,
    float BarWidth,
    float Indent,
    float Padding,
    float Spacing);
