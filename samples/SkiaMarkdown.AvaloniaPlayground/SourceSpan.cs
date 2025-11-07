namespace SkiaMarkdown.AvaloniaPlayground;

/// <summary>
/// Represents a zero-based range in the markdown source buffer.
/// </summary>
public readonly record struct SourceSpan(int Start, int Length)
{
    public int End => Start + Length;

    public bool IsEmpty => Length <= 0;
}
