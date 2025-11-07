namespace SkiaMarkdown.Core.Syntax;

/// <summary>
/// Represents the source location for a Markdown node using 1-based line and column indices.
/// </summary>
public readonly record struct MarkdownSourceMap(int StartLine, int StartColumn, int EndLine, int EndColumn)
{
    public static readonly MarkdownSourceMap Empty = new(1, 1, 1, 1);
}
