using System.Collections.Generic;
using SkiaMarkdown.Core.Buffers;

namespace SkiaMarkdown.Core.Syntax;

/// <summary>
/// Represents a parsed Markdown document.
/// </summary>
public sealed record class MarkdownDocument : MarkdownBlock, IDisposable
{
    private readonly PooledCharBuffer _buffer;
    private bool _disposed;

    public MarkdownDocument(PooledCharBuffer buffer, IReadOnlyList<MarkdownBlock> blocks)
        : base(MarkdownNodeKind.Document, new TextSpan(0, buffer?.Length ?? 0))
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
    }

    public IReadOnlyList<MarkdownBlock> Blocks { get; init; }

    public ReadOnlyMemory<char> Source => _buffer.Memory[..Span.Length];

    public override IEnumerable<MarkdownNode> EnumerateChildren() => Blocks;

    public ReadOnlySpan<char> GetText(TextSpan span)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MarkdownDocument));
        }

        return Source.Span[span.Start..(span.Start + span.Length)];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _buffer.Dispose();
        _disposed = true;
    }
}
