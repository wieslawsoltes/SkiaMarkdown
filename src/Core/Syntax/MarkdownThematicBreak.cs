namespace SkiaMarkdown.Core.Syntax;

public sealed record MarkdownThematicBreak(TextSpan Span)
    : MarkdownBlock(MarkdownNodeKind.ThematicBreak, Span);
