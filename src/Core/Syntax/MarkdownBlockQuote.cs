using System.Collections.Generic;

namespace SkiaMarkdown.Core.Syntax;

public sealed record MarkdownBlockQuote(TextSpan Span, int Depth, IReadOnlyList<MarkdownBlock> Blocks)
    : MarkdownBlock(MarkdownNodeKind.BlockQuote, Span)
{
    public override IEnumerable<MarkdownNode> EnumerateChildren() => Blocks;
}
