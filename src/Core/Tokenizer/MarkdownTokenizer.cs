using System;
using SkiaMarkdown.Core.Syntax;

namespace SkiaMarkdown.Core.Tokenizer;

/// <summary>
/// High-performance tokenizer that yields GitHub Flavored Markdown block-level tokens.
/// </summary>
public ref struct MarkdownTokenizer
{
    private const int OrderedListTaskShift = 24;
    private const int OrderedListTaskMask = unchecked((int)0xFF000000);
    private const int OrderedListNumberMask = 0x00FFFFFF;

    private readonly ReadOnlySpan<char> _source;
    private readonly bool _enableGitHubExtensions;
    private readonly int _baseOffset;
    private readonly int _baseLine;
    private int _position;
    private int _line;

    public MarkdownTokenizer(ReadOnlySpan<char> source, bool enableGitHubExtensions, int baseOffset = 0, int baseLine = 0)
    {
        _source = source;
        _enableGitHubExtensions = enableGitHubExtensions;
        _baseOffset = baseOffset;
        _baseLine = baseLine;
        _position = 0;
        _line = 0;
    }

    public bool TryRead(out MarkdownToken token)
    {
        if (_position >= _source.Length)
        {
            token = MarkdownToken.End(new TextSpan(_baseOffset + _source.Length, 0), _baseLine + _line, 0);
            return false;
        }

        var lineNumber = _line;
        var info = ReadLineInfo(_source, _position, _baseOffset);
        token = default;

        if (info.IsBlank)
        {
            Advance(info);
            token = new MarkdownToken(MarkdownTokenKind.BlankLine, new TextSpan(info.AbsoluteStart, info.TotalLength), _baseLine + lineNumber, info.Column, 0);
            return true;
        }

        if (TryReadFencedCodeBlock(info, lineNumber, ref token) ||
            TryReadHtmlBlock(info, lineNumber, ref token) ||
            TryReadCustomContainer(info, lineNumber, ref token))
        {
            return true;
        }

        if (TryReadHeading(info, lineNumber, out token) ||
            TryReadThematicBreak(info, lineNumber, out token) ||
            TryReadBlockQuote(info, lineNumber, out token) ||
            TryReadListItem(info, lineNumber, out token) ||
            (_enableGitHubExtensions && TryReadTable(info, lineNumber, out token)))
        {
            Advance(info);
            return true;
        }

        Advance(info);
        token = new MarkdownToken(MarkdownTokenKind.Text, new TextSpan(info.AbsoluteStart, info.TotalLength), _baseLine + lineNumber, info.Column, 0);
        return true;
    }

    private void Advance(LineInfo info)
    {
        _position = info.LocalNextPosition;
        _line++;
    }

    private bool TryReadHeading(LineInfo info, int lineNumber, out MarkdownToken token)
    {
        var trimmed = info.Trimmed.TrimEnd();
        var level = 0;

        while (level < trimmed.Length && trimmed[level] == '#')
        {
            level++;
        }

        if (level == 0 || level > 6)
        {
            token = default;
            return false;
        }

        if (level < trimmed.Length && trimmed[level] == ' ')
        {
            token = new MarkdownToken(
                MarkdownTokenKind.Heading,
                new TextSpan(info.AbsoluteStart, info.TotalLength),
                _baseLine + lineNumber,
                info.Column,
                level);
            return true;
        }

        token = default;
        return false;
    }

    private bool TryReadFencedCodeBlock(LineInfo info, int lineNumber, ref MarkdownToken token)
    {
        if (!IsFenceDelimiter(info.Trimmed, out var marker, out var length))
        {
            return false;
        }

        var currentPosition = info.LocalNextPosition;
        var currentLine = lineNumber + 1;

        while (currentPosition < _source.Length)
        {
            var innerInfo = ReadLineInfo(_source, currentPosition, _baseOffset);
            if (IsFenceDelimiter(innerInfo.Trimmed, out var otherMarker, out var otherLength) && otherMarker == marker && otherLength >= length)
            {
                token = new MarkdownToken(
                    MarkdownTokenKind.FencedCodeBlock,
                    TextSpan.FromBounds(info.AbsoluteStart, innerInfo.AbsoluteNextPosition),
                    _baseLine + lineNumber,
                    info.Column,
                    EncodeFence(marker, length));

                _position = innerInfo.LocalNextPosition;
                _line = currentLine + 1;
                return true;
            }

            currentPosition = innerInfo.LocalNextPosition;
            currentLine++;
        }

        token = new MarkdownToken(
            MarkdownTokenKind.FencedCodeBlock,
            TextSpan.FromBounds(info.AbsoluteStart, _baseOffset + _source.Length),
            _baseLine + lineNumber,
            info.Column,
            EncodeFence(marker, length));

        _position = _source.Length;
        _line = currentLine;
        return true;
    }

    private bool TryReadThematicBreak(LineInfo info, int lineNumber, out MarkdownToken token)
    {
        var trimmed = info.Trimmed;
        var marker = '\0';
        var count = 0;

        for (var i = 0; i < trimmed.Length; i++)
        {
            var ch = trimmed[i];
            if (ch is '-' or '*' or '_')
            {
                if (marker == '\0')
                {
                    marker = ch;
                }

                if (ch == marker)
                {
                    count++;
                }
            }
            else if (!char.IsWhiteSpace(ch))
            {
                token = default;
                return false;
            }
        }

        if (count >= 3)
        {
            token = new MarkdownToken(MarkdownTokenKind.ThematicBreak, new TextSpan(info.AbsoluteStart, info.TotalLength), _baseLine + lineNumber, info.Column, count);
            return true;
        }

        token = default;
        return false;
    }

    private bool TryReadBlockQuote(LineInfo info, int lineNumber, out MarkdownToken token)
    {
        var trimmed = info.Trimmed;
        if (trimmed.IsEmpty || trimmed[0] != '>')
        {
            token = default;
            return false;
        }

        var depth = 0;
        var index = 0;
        while (index < trimmed.Length && trimmed[index] == '>')
        {
            depth++;
            index++;
            if (index < trimmed.Length && trimmed[index] == ' ')
            {
                index++;
            }
        }

        token = new MarkdownToken(MarkdownTokenKind.BlockQuote, new TextSpan(info.AbsoluteStart, info.TotalLength), _baseLine + lineNumber, info.Column, depth);
        return true;
    }

    private bool TryReadHtmlBlock(LineInfo info, int lineNumber, ref MarkdownToken token)
    {
        if (!IsHtmlBlockStart(info.Trimmed))
        {
            return false;
        }

        var start = info;
        var currentPosition = info.LocalNextPosition;
        var currentLine = lineNumber + 1;

        while (currentPosition < _source.Length)
        {
            var nextInfo = ReadLineInfo(_source, currentPosition, _baseOffset);
            if (nextInfo.IsBlank)
            {
                currentPosition = nextInfo.LocalNextPosition;
                currentLine++;
                break;
            }

            if (IsHtmlBlockTermination(nextInfo.Trimmed))
            {
                currentPosition = nextInfo.LocalNextPosition;
                currentLine++;
                break;
            }

            currentPosition = nextInfo.LocalNextPosition;
            currentLine++;
        }

        token = new MarkdownToken(
            MarkdownTokenKind.HtmlBlock,
            TextSpan.FromBounds(start.AbsoluteStart, _baseOffset + currentPosition),
            _baseLine + lineNumber,
            info.Column,
            0);

        _position = currentPosition;
        _line = currentLine;
        return true;
    }

    private bool TryReadCustomContainer(LineInfo info, int lineNumber, ref MarkdownToken token)
    {
        var trimmed = info.Trimmed;
        if (!trimmed.StartsWith(":::", StringComparison.Ordinal))
        {
            return false;
        }

        var currentPosition = info.LocalNextPosition;
        var currentLine = lineNumber + 1;

        while (currentPosition < _source.Length)
        {
            var nextInfo = ReadLineInfo(_source, currentPosition, _baseOffset);
            if (nextInfo.Trimmed.StartsWith(":::", StringComparison.Ordinal))
            {
                currentPosition = nextInfo.LocalNextPosition;
                currentLine++;
                token = new MarkdownToken(
                    MarkdownTokenKind.CustomContainer,
                    TextSpan.FromBounds(info.AbsoluteStart, _baseOffset + currentPosition),
                    _baseLine + lineNumber,
                    info.Column,
                    0);

                _position = currentPosition;
                _line = currentLine;
                return true;
            }

            currentPosition = nextInfo.LocalNextPosition;
            currentLine++;
        }

        token = new MarkdownToken(
            MarkdownTokenKind.CustomContainer,
            TextSpan.FromBounds(info.AbsoluteStart, _baseOffset + _source.Length),
            _baseLine + lineNumber,
            info.Column,
            0);

        _position = _source.Length;
        _line = currentLine;
        return true;
    }

    private bool TryReadListItem(LineInfo info, int lineNumber, out MarkdownToken token)
    {
        var trimmed = info.Trimmed;
        if (trimmed.IsEmpty)
        {
            token = default;
            return false;
        }

        if (IsUnorderedListMarker(trimmed, out var markerEnd))
        {
            var taskState = ParseTaskState(trimmed[markerEnd..], out _);
            token = new MarkdownToken(
                MarkdownTokenKind.UnorderedListItem,
                new TextSpan(info.AbsoluteStart, info.TotalLength),
                _baseLine + lineNumber,
                info.Column,
                (int)taskState);
            return true;
        }

        if (IsOrderedListMarker(trimmed, out var number, out var markerEndIndex))
        {
            var taskState = ParseTaskState(trimmed[markerEndIndex..], out _);
            token = new MarkdownToken(
                MarkdownTokenKind.OrderedListItem,
                new TextSpan(info.AbsoluteStart, info.TotalLength),
                _baseLine + lineNumber,
                info.Column,
                EncodeOrderedListValue(number, taskState));
            return true;
        }

        token = default;
        return false;
    }

    private bool TryReadTable(LineInfo info, int lineNumber, out MarkdownToken token)
    {
        var trimmed = info.Trimmed;
        if (trimmed.IndexOf('|') < 0)
        {
            token = default;
            return false;
        }

        if (trimmed.IndexOf('-') < 0)
        {
            token = default;
            return false;
        }

        token = new MarkdownToken(MarkdownTokenKind.Table, new TextSpan(info.AbsoluteStart, info.TotalLength), _baseLine + lineNumber, info.Column, 0);
        return true;
    }

    private static bool IsUnorderedListMarker(ReadOnlySpan<char> span, out int markerEnd)
    {
        markerEnd = 0;
        if (span.IsEmpty)
        {
            return false;
        }

        var marker = span[0];
        if (marker is not ('-' or '*' or '+'))
        {
            return false;
        }

        if (span.Length < 2 || span[1] is not (' ' or '\t'))
        {
            return false;
        }

        var index = 2;
        while (index < span.Length && span[index] is ' ' or '\t')
        {
            index++;
        }

        markerEnd = index;
        return true;
    }

    private static bool IsOrderedListMarker(ReadOnlySpan<char> span, out int number, out int markerEnd)
    {
        number = 0;
        markerEnd = 0;
        var index = 0;
        var digitCount = 0;

        while (index < span.Length && char.IsDigit(span[index]))
        {
            number = (number * 10) + (span[index] - '0');
            digitCount++;
            if (digitCount > 9)
            {
                return false;
            }

            index++;
        }

        if (digitCount == 0 || index >= span.Length)
        {
            return false;
        }

        var terminator = span[index];
        if (terminator is '.' or ')')
        {
            index++;
            if (index < span.Length && (span[index] == ' ' || span[index] == '\t'))
            {
                index++;
                while (index < span.Length && span[index] is ' ' or '\t')
                {
                    index++;
                }

                markerEnd = index;
                return true;
            }
        }

        return false;
    }

    private static MarkdownTaskState ParseTaskState(ReadOnlySpan<char> span, out int consumed)
    {
        consumed = 0;
        if (span.Length < 3 || span[0] != '[' || span[2] != ']')
        {
            return MarkdownTaskState.None;
        }

        var marker = char.ToLowerInvariant(span[1]);
        consumed = 3;
        if (span.Length > consumed && span[consumed] == ' ')
        {
            consumed++;
        }

        return marker == 'x' ? MarkdownTaskState.Complete : MarkdownTaskState.Incomplete;
    }

    private static bool IsFenceDelimiter(ReadOnlySpan<char> span, out char marker, out int length)
    {
        marker = '\0';
        length = 0;
        if (span.Length < 3)
        {
            return false;
        }

        var first = span[0];
        if (first is not ('`' or '~'))
        {
            return false;
        }

        var count = 0;
        while (count < span.Length && span[count] == first)
        {
            count++;
        }

        if (count < 3)
        {
            return false;
        }

        marker = first;
        length = count;
        return true;
    }

    internal static int EncodeOrderedListValue(int number, MarkdownTaskState taskState)
    {
        number = Math.Clamp(number, 0, OrderedListNumberMask);
        return (int)taskState << OrderedListTaskShift | (number & OrderedListNumberMask);
    }

    internal static void DecodeOrderedListValue(int value, out int number, out MarkdownTaskState taskState)
    {
        taskState = (MarkdownTaskState)((value & OrderedListTaskMask) >> OrderedListTaskShift);
        number = value & OrderedListNumberMask;
        if (number == 0)
        {
            number = 1;
        }
    }

    private static int CountLeadingWhitespace(ReadOnlySpan<char> span)
    {
        var count = 0;
        while (count < span.Length && (span[count] == ' ' || span[count] == '\t'))
        {
            count++;
        }

        return count;
    }

    private static int EncodeFence(char marker, int length) => (marker << 16) | length;

    internal static void DecodeFence(int value, out char marker, out int length)
    {
        marker = (char)(value >> 16);
        length = value & 0xFFFF;
    }

    private readonly ref struct LineInfo
    {
        public LineInfo(int localStart, int absoluteStart, int length, int newlineLength, int column, ReadOnlySpan<char> content, ReadOnlySpan<char> trimmed, bool isBlank)
        {
            LocalStart = localStart;
            AbsoluteStart = absoluteStart;
            Length = length;
            NewlineLength = newlineLength;
            Column = column;
            Content = content;
            Trimmed = trimmed;
            IsBlank = isBlank;
        }

        public int LocalStart { get; }

        public int AbsoluteStart { get; }

        public int Length { get; }

        public int NewlineLength { get; }

        public int TotalLength => Length + NewlineLength;

        public int Column { get; }

        public ReadOnlySpan<char> Content { get; }

        public ReadOnlySpan<char> Trimmed { get; }

        public int LocalNextPosition => LocalStart + TotalLength;

        public int AbsoluteNextPosition => AbsoluteStart + TotalLength;

        public bool IsBlank { get; }
    }

    private static bool IsHtmlBlockStart(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty || span[0] != '<')
        {
            return false;
        }

        if (span.StartsWith("<!--", StringComparison.Ordinal))
        {
            return true;
        }

        if (span.StartsWith("</", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var tag in HtmlBlockStarters)
        {
            if (span.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHtmlBlockTermination(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return true;
        }

        if (span.IndexOf("-->", StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        foreach (var tag in HtmlBlockTerminators)
        {
            if (span.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static LineInfo ReadLineInfo(ReadOnlySpan<char> source, int position, int baseOffset)
    {
        var remaining = source[position..];
        var length = 0;
        var newlineLength = 0;

        for (var i = 0; i < remaining.Length; i++)
        {
            var ch = remaining[i];
            if (ch == '\r')
            {
                length = i;
                newlineLength = i + 1 < remaining.Length && remaining[i + 1] == '\n' ? 2 : 1;
                goto Exit;
            }

            if (ch == '\n')
            {
                length = i;
                newlineLength = 1;
                goto Exit;
            }
        }

        length = remaining.Length;

Exit:
        var content = remaining[..length];
        var column = CountLeadingWhitespace(content);
        var trimmed = column < content.Length ? content[column..] : ReadOnlySpan<char>.Empty;
        var absoluteStart = baseOffset + position;
        var isBlank = content.Trim().IsEmpty;
        return new LineInfo(position, absoluteStart, length, newlineLength, column, content, trimmed, isBlank);
    }

    private static readonly string[] HtmlBlockStarters =
    {
        "<address",
        "<article",
        "<aside",
        "<base",
        "<basefont",
        "<blockquote",
        "<body",
        "<caption",
        "<center",
        "<col",
        "<colgroup",
        "<dd",
        "<details",
        "<dialog",
        "<div",
        "<dl",
        "<dt",
        "<fieldset",
        "<figcaption",
        "<figure",
        "<footer",
        "<form",
        "<frame",
        "<frameset",
        "<h1",
        "<h2",
        "<h3",
        "<h4",
        "<h5",
        "<h6",
        "<head",
        "<header",
        "<hr",
        "<html",
        "<iframe",
        "<legend",
        "<li",
        "<link",
        "<main",
        "<menu",
        "<menuitem",
        "<nav",
        "<noframes",
        "<ol",
        "<optgroup",
        "<option",
        "<p",
        "<pre",
        "<script",
        "<section",
        "<source",
        "<style",
        "<summary",
        "<table",
        "<tbody",
        "<td",
        "<tfoot",
        "<th",
        "<thead",
        "<title",
        "<tr",
        "<track",
        "<ul"
    };

    private static readonly string[] HtmlBlockTerminators =
    {
        "</address",
        "</article",
        "</aside",
        "</blockquote",
        "</body",
        "</details",
        "</dialog",
        "</div",
        "</dl",
        "</figure",
        "</footer",
        "</form",
        "</header",
        "</html",
        "</li",
        "</main",
        "</menu",
        "</nav",
        "</ol",
        "</pre",
        "</script",
        "</section",
        "</summary",
        "</table",
        "</tbody",
        "</td",
        "</tfoot",
        "</th",
        "</thead",
        "</tr",
        "</ul"
    };
}
