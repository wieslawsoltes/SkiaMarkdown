namespace SkiaMarkdown.Core.Syntax;

/// <summary>
/// Enumerates Markdown syntax node kinds.
/// </summary>
public enum MarkdownNodeKind
{
    Document,
    Paragraph,
    Heading,
    Text,
    List,
    ListItem,
    BlockQuote,
    CodeBlock,
    ThematicBreak,
    Table,
    TableRow,
    TableCell,
    HtmlBlock,
    CustomContainer
}
