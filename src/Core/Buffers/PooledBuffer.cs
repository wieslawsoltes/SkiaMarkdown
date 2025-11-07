using System.Buffers;
using System.Runtime.CompilerServices;

namespace SkiaMarkdown.Core.Buffers;

/// <summary>
/// Provides a pooled buffer backed by <see cref="ArrayPool{T}"/> with dynamic growth.
/// </summary>
public sealed class PooledBuffer<T> : IDisposable
{
    private T[] _buffer;

    public PooledBuffer(int initialCapacity = 128)
    {
        if (initialCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        _buffer = ArrayPool<T>.Shared.Rent(initialCapacity);
        Count = 0;
    }

    public int Count { get; private set; }

    public Span<T> Span => _buffer.AsSpan(0, Count);

    public ReadOnlySpan<T> ReadOnlySpan => _buffer.AsSpan(0, Count);

    public ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref _buffer[index];
        }
    }

    public void Add(T item)
    {
        if (Count == _buffer.Length)
        {
            Grow();
        }

        _buffer[Count++] = item;
    }

    public T[] ToArray()
    {
        var result = new T[Count];
        Span.CopyTo(result);
        return result;
    }

    public void Reset(bool clear = false)
    {
        if (clear)
        {
            Span.Clear();
        }

        Count = 0;
    }

    public void Dispose()
    {
        var buffer = _buffer;
        _buffer = Array.Empty<T>();
        if (buffer.Length > 0)
        {
            ArrayPool<T>.Shared.Return(buffer, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Grow()
    {
        var newSize = _buffer.Length * 2;
        var newBuffer = ArrayPool<T>.Shared.Rent(newSize);
        Span.CopyTo(newBuffer.AsSpan());
        ArrayPool<T>.Shared.Return(_buffer, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        _buffer = newBuffer;
    }
}
