namespace SkiaMarkdown.Core.Syntax;

public sealed record MarkdownCodeBlock(TextSpan Span, string? Info, TextSpan ContentSpan)
    : MarkdownBlock(MarkdownNodeKind.CodeBlock, Span);
