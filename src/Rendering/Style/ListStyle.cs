namespace SkiaMarkdown.Rendering.Style;

public sealed record ListStyle(
    TextStyle Text,
    float Indent,
    float MarkerSpacing,
    float ItemSpacing,
    float Spacing);
