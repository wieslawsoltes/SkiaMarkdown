using System.Collections.Generic;

namespace SkiaMarkdown.Core.Syntax;

public sealed record MarkdownCustomContainer(TextSpan Span, string Info, IReadOnlyList<MarkdownBlock> Blocks)
    : MarkdownBlock(MarkdownNodeKind.CustomContainer, Span)
{
    public override IEnumerable<MarkdownNode> EnumerateChildren() => Blocks;
}
