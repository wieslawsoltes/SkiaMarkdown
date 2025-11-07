using System.Collections.Generic;

namespace SkiaMarkdown.Core.Syntax;

public sealed record MarkdownTable(TextSpan Span, IReadOnlyList<MarkdownTableRow> Rows, IReadOnlyList<MarkdownTableAlignment> Alignments)
    : MarkdownBlock(MarkdownNodeKind.Table, Span)
{
    public override IEnumerable<MarkdownNode> EnumerateChildren() => Rows;
}

public sealed record MarkdownTableRow(TextSpan Span, IReadOnlyList<MarkdownTableCell> Cells, bool IsHeader)
    : MarkdownNode(MarkdownNodeKind.TableRow, Span)
{
    public override IEnumerable<MarkdownNode> EnumerateChildren() => Cells;
}

public sealed record MarkdownTableCell(TextSpan Span)
    : MarkdownNode(MarkdownNodeKind.TableCell, Span);
