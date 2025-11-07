namespace SkiaMarkdown.Rendering.Style;

/// <summary>
/// Provides options for the layout engine.
/// </summary>
public sealed record LayoutOptions
{
    public static LayoutOptions Default { get; } = new();

    public float CanvasWidth { get; init; } = 960f;

    public RendererStyle Style { get; init; } = RendererStyle.GitHubLight;
}
