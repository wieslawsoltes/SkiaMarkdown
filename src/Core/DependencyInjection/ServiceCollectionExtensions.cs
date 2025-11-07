using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaMarkdown.Core.Configuration;
using SkiaMarkdown.Core.Extensibility;
using SkiaMarkdown.Core.Pipeline;
using SkiaMarkdown.Core.Parsing;

namespace SkiaMarkdown.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private const string MeterName = "SkiaMarkdown.Core";

    /// <summary>
    /// Registers the SkiaMarkdown core services.
    /// </summary>
    public static SkiaMarkdownCoreBuilder AddSkiaMarkdownCore(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddOptions<MarkdownOptions>();

        services.TryAddSingleton<MarkdownParser>();

        services.TryAddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<MarkdownOptions>>().Value;
            var logger = provider.GetService<ILogger<MarkdownPipeline>>() ?? NullLogger<MarkdownPipeline>.Instance;
            var parser = provider.GetRequiredService<MarkdownParser>();
            var extensions = provider.GetServices<IMarkdownExtension>();
            return new MarkdownPipeline(options, logger, parser, extensions);
        });

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMeterFactory, MeterProvider>());

        return new SkiaMarkdownCoreBuilder(services);
    }

    private sealed class MeterProvider : IMeterFactory, IDisposable
    {
        private readonly List<Meter> _meters = new();

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options.Name ?? MeterName, options.Version, options.Tags);
            lock (_meters)
            {
                _meters.Add(meter);
            }

            return meter;
        }

        public void Dispose()
        {
            lock (_meters)
            {
                foreach (var meter in _meters)
                {
                    meter.Dispose();
                }

                _meters.Clear();
            }
        }
    }
}
