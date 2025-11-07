namespace SkiaMarkdown.Core.Syntax;

public sealed record MarkdownText(TextSpan Span)
    : MarkdownInline(MarkdownNodeKind.Text, Span);
