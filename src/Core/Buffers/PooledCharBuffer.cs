using System.Buffers;

namespace SkiaMarkdown.Core.Buffers;

/// <summary>
/// Provides pooled character storage for Markdown source text.
/// </summary>
public sealed class PooledCharBuffer : IMemoryOwner<char>
{
    private char[]? _buffer;

    public PooledCharBuffer(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _buffer = length == 0 ? Array.Empty<char>() : ArrayPool<char>.Shared.Rent(length);
        Length = length;
    }

    public int Length { get; }

    public Memory<char> Memory
    {
        get
        {
            var buffer = _buffer ?? throw new ObjectDisposedException(nameof(PooledCharBuffer));
            return buffer.AsMemory(0, Length);
        }
    }

    public Span<char> Span
    {
        get
        {
            var buffer = _buffer ?? throw new ObjectDisposedException(nameof(PooledCharBuffer));
            return buffer.AsSpan(0, Length);
        }
    }

    public void Dispose()
    {
        var buffer = _buffer;
        if (buffer is null)
        {
            return;
        }

        _buffer = null;
        if (buffer.Length > 0)
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}
