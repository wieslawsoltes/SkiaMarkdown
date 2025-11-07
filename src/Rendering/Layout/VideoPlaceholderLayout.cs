using SkiaSharp;

namespace SkiaMarkdown.Rendering.Layout;

/// <summary>
/// Represents a video placeholder surface with optional poster image.
/// </summary>
public sealed class VideoPlaceholderLayout : LayoutElement
{
    public VideoPlaceholderLayout(SKRect bounds, string source, string? title, string? posterSource)
        : base(bounds)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Title = title;
        PosterSource = posterSource;
    }

    public string Source { get; }

    public string? Title { get; }

    public string? PosterSource { get; }
}
