using System.Collections.Generic;

namespace SkiaMarkdown.Core.Extensibility;

public sealed class MarkdownPipelineBuilder
{
    internal IList<IMarkdownTokenFilter> TokenFilters { get; } = new List<IMarkdownTokenFilter>();

    internal IList<IMarkdownDocumentTransformer> DocumentTransformers { get; } = new List<IMarkdownDocumentTransformer>();

    public MarkdownPipelineBuilder AddTokenFilter(IMarkdownTokenFilter filter)
    {
        if (filter is null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        TokenFilters.Add(filter);
        return this;
    }

    public MarkdownPipelineBuilder AddDocumentTransformer(IMarkdownDocumentTransformer transformer)
    {
        if (transformer is null)
        {
            throw new ArgumentNullException(nameof(transformer));
        }

        DocumentTransformers.Add(transformer);
        return this;
    }

    public MarkdownPipelineBuilder AddExtension(IMarkdownExtension extension)
    {
        if (extension is null)
        {
            throw new ArgumentNullException(nameof(extension));
        }

        extension.Configure(this);
        return this;
    }
}
