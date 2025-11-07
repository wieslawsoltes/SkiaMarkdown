using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkiaMarkdown.Core.Configuration;
using SkiaMarkdown.Core.Extensibility;
using SkiaMarkdown.Core.Pipeline;

namespace SkiaMarkdown.Core.DependencyInjection;

/// <summary>
/// Provides a fluent customization layer for wiring the core Markdown pipeline.
/// </summary>
public sealed class SkiaMarkdownCoreBuilder
{
    public SkiaMarkdownCoreBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Replaces the default <see cref="MarkdownOptions"/> instance.
    /// </summary>
    public SkiaMarkdownCoreBuilder Configure(Action<MarkdownOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        Services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Adds a custom logger provider for the core pipeline.
    /// </summary>
    public SkiaMarkdownCoreBuilder ConfigureLogging(Action<ILoggingBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        Services.AddLogging(configure);
        return this;
    }

    /// <summary>
    /// Registers a Markdown extension type.
    /// </summary>
    public SkiaMarkdownCoreBuilder AddExtension<TExtension>()
        where TExtension : class, IMarkdownExtension
    {
        Services.AddSingleton<IMarkdownExtension, TExtension>();
        return this;
    }

    /// <summary>
    /// Registers a Markdown extension instance.
    /// </summary>
    public SkiaMarkdownCoreBuilder AddExtension(IMarkdownExtension extension)
    {
        if (extension is null)
        {
            throw new ArgumentNullException(nameof(extension));
        }

        Services.AddSingleton(extension);
        return this;
    }
}
