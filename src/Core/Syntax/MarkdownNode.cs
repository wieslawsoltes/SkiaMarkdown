using System.Collections.Generic;

namespace SkiaMarkdown.Core.Syntax;

/// <summary>
/// Base type for all Markdown syntax nodes.
/// </summary>
public abstract record MarkdownNode(MarkdownNodeKind Kind, TextSpan Span)
{
    public MarkdownSourceMap SourceMap { get; init; }

    public virtual IEnumerable<MarkdownNode> EnumerateChildren()
    {
        yield break;
    }
}

public abstract record MarkdownBlock(MarkdownNodeKind Kind, TextSpan Span) : MarkdownNode(Kind, Span);

public abstract record MarkdownInline(MarkdownNodeKind Kind, TextSpan Span) : MarkdownNode(Kind, Span);
