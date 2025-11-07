namespace SkiaMarkdown.Core.Tokenizer;

/// <summary>
/// Identifies the type of token encountered in the Markdown source.
/// </summary>
public enum MarkdownTokenKind : ushort
{
    None = 0,
    EndOfFile,
    BlankLine,
    Heading,
    Paragraph,
    BlockQuote,
    FencedCodeBlock,
    IndentedCodeBlock,
    OrderedListItem,
    UnorderedListItem,
    ThematicBreak,
    HtmlBlock,
    CustomContainer,
    Table,
    Text
}
