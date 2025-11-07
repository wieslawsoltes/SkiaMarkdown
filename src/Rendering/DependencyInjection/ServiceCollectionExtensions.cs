using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaMarkdown.Core.DependencyInjection;
using SkiaMarkdown.Rendering;

namespace SkiaMarkdown.Rendering.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the rendering layer for the SkiaMarkdown pipeline.
    /// </summary>
    public static SkiaMarkdownRenderingBuilder AddSkiaMarkdownRendering(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddSkiaMarkdownCore();
        services.TryAddSingleton<IMediaResourceProvider>(_ => NullMediaResourceProvider.Instance);
        services.TryAddSingleton<MarkdownRenderer>();

        return new SkiaMarkdownRenderingBuilder(services);
    }
}
