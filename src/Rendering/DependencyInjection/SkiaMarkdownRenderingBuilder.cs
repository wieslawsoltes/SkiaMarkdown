using Microsoft.Extensions.DependencyInjection;

namespace SkiaMarkdown.Rendering.DependencyInjection;

/// <summary>
/// Fluent builder for configuring rendering services.
/// </summary>
public sealed class SkiaMarkdownRenderingBuilder
{
    public SkiaMarkdownRenderingBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IServiceCollection Services { get; }
}
