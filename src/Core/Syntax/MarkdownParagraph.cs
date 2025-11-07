using System.Collections.Generic;

namespace SkiaMarkdown.Core.Syntax;

public sealed record MarkdownParagraph(TextSpan Span, IReadOnlyList<MarkdownInline> Inlines)
    : MarkdownBlock(MarkdownNodeKind.Paragraph, Span)
{
    public override IEnumerable<MarkdownNode> EnumerateChildren() => Inlines;
}
