namespace SkiaMarkdown.Core.Syntax;

/// <summary>
/// Represents a segment within the original Markdown source.
/// </summary>
public readonly record struct TextSpan(int Start, int Length)
{
    public static TextSpan FromBounds(int start, int end)
    {
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        return new TextSpan(start, end - start);
    }

    public bool Contains(int position) => position >= Start && position < Start + Length;

    public override string ToString() => $"[{Start}..{Start + Length})";
}
