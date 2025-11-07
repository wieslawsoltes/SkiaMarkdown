namespace SkiaMarkdown.Core.Syntax;

public sealed record MarkdownHtmlBlock(TextSpan Span)
    : MarkdownBlock(MarkdownNodeKind.HtmlBlock, Span);
