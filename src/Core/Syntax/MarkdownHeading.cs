using System.Collections.Generic;

namespace SkiaMarkdown.Core.Syntax;

public sealed record MarkdownHeading(TextSpan Span, int Level, IReadOnlyList<MarkdownInline> Inlines)
    : MarkdownBlock(MarkdownNodeKind.Heading, Span)
{
    public override IEnumerable<MarkdownNode> EnumerateChildren() => Inlines;
}
