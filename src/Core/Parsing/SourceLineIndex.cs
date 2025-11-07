using System;
using System.Collections.Generic;

namespace SkiaMarkdown.Core.Parsing;

internal readonly struct SourceLineIndex
{
    private readonly int[] _lineStarts;

    private SourceLineIndex(int[] lineStarts)
    {
        _lineStarts = lineStarts;
    }

    public static SourceLineIndex Create(ReadOnlySpan<char> source)
    {
        if (source.Length == 0)
        {
            return new SourceLineIndex(new[] { 0 });
        }

        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (ch == '\r')
            {
                if (i + 1 < source.Length && source[i + 1] == '\n')
                {
                    lineStarts.Add(i + 2);
                    i++;
                }
                else
                {
                    lineStarts.Add(i + 1);
                }
            }
            else if (ch == '\n')
            {
                lineStarts.Add(i + 1);
            }
        }

        return new SourceLineIndex(lineStarts.ToArray());
    }

    public (int Line, int Column) GetLineAndColumn(int position)
    {
        if (_lineStarts is null || _lineStarts.Length == 0)
        {
            return (1, 1);
        }

        var index = BinarySearch(_lineStarts, position);
        if (index < 0)
        {
            index = ~index - 1;
            if (index < 0)
            {
                index = 0;
            }
        }

        var lineStart = _lineStarts[index];
        return (index + 1, (position - lineStart) + 1);
    }

    private static int BinarySearch(int[] array, int value)
    {
        var low = 0;
        var high = array.Length - 1;

        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            var midValue = array[mid];

            if (midValue == value)
            {
                return mid;
            }

            if (midValue < value)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return ~low;
    }
}
