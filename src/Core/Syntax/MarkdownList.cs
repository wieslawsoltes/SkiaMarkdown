using System.Collections.Generic;

namespace SkiaMarkdown.Core.Syntax;

public sealed record MarkdownList(TextSpan Span, bool IsOrdered, int Start, IReadOnlyList<MarkdownListItem> Items)
    : MarkdownBlock(MarkdownNodeKind.List, Span)
{
    public override IEnumerable<MarkdownNode> EnumerateChildren() => Items;
}

public sealed record MarkdownListItem(TextSpan Span, MarkdownTaskState TaskState, IReadOnlyList<MarkdownBlock> Blocks)
    : MarkdownBlock(MarkdownNodeKind.ListItem, Span)
{
    public override IEnumerable<MarkdownNode> EnumerateChildren() => Blocks;
}
