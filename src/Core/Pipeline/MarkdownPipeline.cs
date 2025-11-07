using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using SkiaMarkdown.Core.Buffers;
using SkiaMarkdown.Core.Configuration;
using SkiaMarkdown.Core.Extensibility;
using SkiaMarkdown.Core.Parsing;
using SkiaMarkdown.Core.Syntax;

namespace SkiaMarkdown.Core.Pipeline;

/// <summary>
/// Coordinates Markdown parsing with extensibility hooks.
/// </summary>
public sealed class MarkdownPipeline
{
    private readonly MarkdownOptions _options;
    private readonly ILogger<MarkdownPipeline> _logger;
    private readonly MarkdownParser _parser;
    private readonly IReadOnlyList<IMarkdownTokenFilter> _tokenFilters;
    private readonly IReadOnlyList<IMarkdownDocumentTransformer> _documentTransformers;

    public MarkdownPipeline(
        MarkdownOptions options,
        ILogger<MarkdownPipeline> logger,
        MarkdownParser parser,
        IEnumerable<IMarkdownExtension> extensions)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        var extensionList = extensions?.ToList() ?? new List<IMarkdownExtension>();
        var builder = new MarkdownPipelineBuilder();
        foreach (var extension in extensionList)
        {
            builder.AddExtension(extension);
        }

        _tokenFilters = builder.TokenFilters.ToArray();
        _documentTransformers = builder.DocumentTransformers.ToArray();
    }

    public MarkdownDocument Parse(ReadOnlySpan<char> markdown)
    {
        if (markdown.Length * sizeof(char) >= _options.StreamingThresholdBytes)
        {
            _logger.LogDebug("Markdown payload length {Length} exceeds streaming threshold {Threshold}.", markdown.Length, _options.StreamingThresholdBytes);
        }

        var buffer = new PooledCharBuffer(markdown.Length);
        if (markdown.Length > 0)
        {
            markdown.CopyTo(buffer.Span);
        }

        return _parser.Parse(buffer, _options, _tokenFilters, _documentTransformers);
    }
}
