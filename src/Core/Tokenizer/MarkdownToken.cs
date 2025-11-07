using SkiaMarkdown.Core.Syntax;

namespace SkiaMarkdown.Core.Tokenizer;

/// <summary>
/// Describes a lexical token identified by the tokenizer.
/// </summary>
public readonly record struct MarkdownToken(
    MarkdownTokenKind Kind,
    TextSpan Span,
    int Line,
    int Column,
    int Value)
{
    public static MarkdownToken End(TextSpan span, int line, int column) =>
        new(MarkdownTokenKind.EndOfFile, span, line, column, 0);
}
