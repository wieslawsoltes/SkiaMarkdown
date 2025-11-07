using System;
using System.Collections.Generic;
using SkiaMarkdown.Core.Buffers;
using SkiaMarkdown.Core.Configuration;
using SkiaMarkdown.Core.Extensibility;
using SkiaMarkdown.Core.Syntax;
using SkiaMarkdown.Core.Tokenizer;

namespace SkiaMarkdown.Core.Parsing;

public sealed class MarkdownParser
{
    public MarkdownDocument Parse(
        PooledCharBuffer buffer,
        MarkdownOptions options,
        IReadOnlyList<IMarkdownTokenFilter> tokenFilters,
        IReadOnlyList<IMarkdownDocumentTransformer> documentTransformers)
    {
        var sourceMemory = buffer.Memory[..buffer.Length];
        var sourceSpan = sourceMemory.Span;
        var context = new BlockParsingContext(sourceMemory, options, tokenFilters, SourceLineIndex.Create(sourceSpan));

        var blocks = ParseSegment(context, 0, sourceSpan.Length, baseLine: 0);
        var document = new MarkdownDocument(buffer, blocks)
        {
            SourceMap = CreateSourceMap(new TextSpan(0, buffer.Length), context.LineIndex)
        };

        ApplyDocumentTransformers(documentTransformers, document);
        return document;
    }

    private static MarkdownBlock[] ParseSegment(in BlockParsingContext context, int start, int length, int baseLine)
    {
        if (length <= 0)
        {
            return Array.Empty<MarkdownBlock>();
        }

        var segment = context.Source.Span.Slice(start, length);
        var tokens = TokenizeSegment(segment, context, start, baseLine);
        if (tokens.Length == 0)
        {
            return Array.Empty<MarkdownBlock>();
        }

        return BuildBlocks(context, tokens);
    }

    private static MarkdownToken[] TokenizeSegment(ReadOnlySpan<char> segment, in BlockParsingContext context, int baseOffset, int baseLine)
    {
        var tokenizer = new MarkdownTokenizer(segment, context.Options.EnableGitHubExtensions, baseOffset, baseLine);
        using var tokenBuffer = new PooledBuffer<MarkdownToken>(Math.Max(16, segment.Length / 32));

        while (tokenizer.TryRead(out var token))
        {
            var filtered = ApplyTokenFilters(context.Source.Span, context.TokenFilters, token);
            if (filtered.Kind == MarkdownTokenKind.None)
            {
                continue;
            }

            tokenBuffer.Add(filtered);
        }

        return tokenBuffer.ToArray();
    }

    private static MarkdownBlock[] BuildBlocks(in BlockParsingContext context, MarkdownToken[] tokens)
    {
        using var blocks = new PooledBuffer<MarkdownBlock>(Math.Max(16, tokens.Length));
        var index = 0;
        var source = context.Source.Span;

        while (index < tokens.Length)
        {
            var token = tokens[index];
            switch (token.Kind)
            {
                case MarkdownTokenKind.BlankLine:
                    index++;
                    break;
                case MarkdownTokenKind.Heading:
                    blocks.Add(ParseHeading(context, token));
                    index++;
                    break;
                case MarkdownTokenKind.Text:
                    blocks.Add(ParseParagraph(context, tokens, ref index));
                    break;
                case MarkdownTokenKind.ThematicBreak:
                    var thematic = new MarkdownThematicBreak(token.Span)
                    {
                        SourceMap = CreateSourceMap(token.Span, context.LineIndex)
                    };
                    blocks.Add(thematic);
                    index++;
                    break;
                case MarkdownTokenKind.FencedCodeBlock:
                    blocks.Add(ParseFencedCodeBlock(context, token));
                    index++;
                    break;
                case MarkdownTokenKind.OrderedListItem:
                case MarkdownTokenKind.UnorderedListItem:
                    blocks.Add(ParseList(context, tokens, ref index));
                    break;
                case MarkdownTokenKind.BlockQuote:
                    blocks.Add(ParseBlockQuote(context, tokens, ref index));
                    break;
                case MarkdownTokenKind.Table:
                    blocks.Add(ParseTable(context, tokens, ref index));
                    break;
                case MarkdownTokenKind.HtmlBlock:
                    blocks.Add(ParseHtmlBlock(context, token));
                    index++;
                    break;
                case MarkdownTokenKind.CustomContainer:
                    blocks.Add(ParseCustomContainer(context, token));
                    index++;
                    break;
                default:
                    // Fallback treat as paragraph
                    blocks.Add(ParseParagraph(context, tokens, ref index));
                    break;
            }
        }

        return blocks.ToArray();
    }

    private static MarkdownHeading ParseHeading(in BlockParsingContext context, MarkdownToken token)
    {
        var source = context.Source.Span;
        var slice = TrimTrailingNewline(source[token.Span.Start..(token.Span.Start + token.Span.Length)]);
        var offset = 0;
        while (offset < slice.Length && slice[offset] == '#')
        {
            offset++;
        }

        if (offset < slice.Length && slice[offset] == ' ')
        {
            offset++;
        }

        var contentSpan = new TextSpan(token.Span.Start + offset, Math.Max(0, slice.Length - offset));
        var inline = new MarkdownText(contentSpan)
        {
            SourceMap = CreateSourceMap(contentSpan, context.LineIndex)
        };

        return new MarkdownHeading(token.Span, token.Value, new[] { inline })
        {
            SourceMap = CreateSourceMap(token.Span, context.LineIndex)
        };
    }

    private static MarkdownParagraph ParseParagraph(in BlockParsingContext context, MarkdownToken[] tokens, ref int index)
    {
        using var inlines = new PooledBuffer<MarkdownInline>();
        var source = context.Source.Span;
        var start = tokens[index].Span.Start;
        var end = start;

        while (index < tokens.Length)
        {
            var token = tokens[index];
            if (token.Kind is MarkdownTokenKind.Text)
            {
                var textSlice = TrimTrailingNewline(source[token.Span.Start..(token.Span.Start + token.Span.Length)]);
                var textSpan = new TextSpan(token.Span.Start, textSlice.Length);
                inlines.Add(new MarkdownText(textSpan)
                {
                    SourceMap = CreateSourceMap(textSpan, context.LineIndex)
                });
                end = token.Span.Start + token.Span.Length;
                index++;
                continue;
            }

            if (token.Kind == MarkdownTokenKind.BlankLine)
            {
                index++;
            }

            break;
        }

        var span = TextSpan.FromBounds(start, end);
        return new MarkdownParagraph(span, inlines.ToArray())
        {
            SourceMap = CreateSourceMap(span, context.LineIndex)
        };
    }

    private static MarkdownCodeBlock ParseFencedCodeBlock(in BlockParsingContext context, MarkdownToken token)
    {
        MarkdownTokenizer.DecodeFence(token.Value, out var marker, out _);
        var source = context.Source.Span;
        var slice = source[token.Span.Start..(token.Span.Start + token.Span.Length)];
        var firstLineLength = GetLineLength(slice);
        var infoSlice = slice[..firstLineLength].Trim();
        var infoString = infoSlice.Length > 0 ? new string(infoSlice) : null;

        var contentStartOffset = firstLineLength;
        if (contentStartOffset < slice.Length && slice[contentStartOffset] == '\r')
        {
            contentStartOffset++;
        }

        if (contentStartOffset < slice.Length && slice[contentStartOffset] == '\n')
        {
            contentStartOffset++;
        }

        var contentEnd = FindClosingFence(slice, marker);
        var contentSpanLength = Math.Max(0, contentEnd - contentStartOffset);
        var contentSpan = new TextSpan(token.Span.Start + contentStartOffset, contentSpanLength);

        return new MarkdownCodeBlock(token.Span, infoString, contentSpan)
        {
            SourceMap = CreateSourceMap(token.Span, context.LineIndex)
        };
    }

    private static MarkdownBlock ParseList(in BlockParsingContext context, MarkdownToken[] tokens, ref int index)
    {
        var source = context.Source.Span;
        var startToken = tokens[index];
        var isOrdered = startToken.Kind == MarkdownTokenKind.OrderedListItem;
        var startNumber = 1;

        if (isOrdered)
        {
            MarkdownTokenizer.DecodeOrderedListValue(startToken.Value, out startNumber, out _);
        }

        using var items = new PooledBuffer<MarkdownListItem>();
        var listStart = startToken.Span.Start;
        var listEnd = startToken.Span.Start + startToken.Span.Length;
        var consumedIndex = index;

        while (consumedIndex < tokens.Length)
        {
            var token = tokens[consumedIndex];
            if (token.Kind != startToken.Kind)
            {
                if (token.Kind == MarkdownTokenKind.BlankLine &&
                    consumedIndex + 1 < tokens.Length &&
                    tokens[consumedIndex + 1].Kind == startToken.Kind)
                {
                    consumedIndex++;
                    continue;
                }

                break;
            }

            items.Add(ParseListItem(context, token, isOrdered));
            listEnd = token.Span.Start + token.Span.Length;
            consumedIndex++;
        }

        index = consumedIndex;
        var span = TextSpan.FromBounds(listStart, listEnd);
        var list = new MarkdownList(span, isOrdered, startNumber, items.ToArray())
        {
            SourceMap = CreateSourceMap(span, context.LineIndex)
        };

        return list;
    }

    private static MarkdownListItem ParseListItem(in BlockParsingContext context, MarkdownToken token, bool isOrdered)
    {
        var source = context.Source.Span;
        var (contentSpan, taskState) = ExtractListItemContent(source, token, isOrdered);
        if (taskState == MarkdownTaskState.None)
        {
            taskState = isOrdered
                ? GetTaskStateFromToken(token.Value)
                : (MarkdownTaskState)token.Value;
        }

        var text = new MarkdownText(contentSpan)
        {
            SourceMap = CreateSourceMap(contentSpan, context.LineIndex)
        };

        var paragraph = new MarkdownParagraph(contentSpan, new MarkdownInline[] { text })
        {
            SourceMap = CreateSourceMap(contentSpan, context.LineIndex)
        };

        return new MarkdownListItem(token.Span, taskState, new MarkdownBlock[] { paragraph })
        {
            SourceMap = CreateSourceMap(token.Span, context.LineIndex)
        };
    }

    private static MarkdownBlock ParseBlockQuote(in BlockParsingContext context, MarkdownToken[] tokens, ref int index)
    {
        var source = context.Source.Span;
        var startToken = tokens[index];
        using var quoteBlocks = new PooledBuffer<MarkdownBlock>();
        var depth = startToken.Value;
        var start = startToken.Span.Start;
        var end = startToken.Span.Start + startToken.Span.Length;

        while (index < tokens.Length && tokens[index].Kind == MarkdownTokenKind.BlockQuote)
        {
            var token = tokens[index];
            depth = Math.Max(depth, token.Value);
            var contentSpan = ExtractBlockQuoteContentSpan(source, token);
            if (contentSpan.Length > 0)
            {
                var text = new MarkdownText(contentSpan)
                {
                    SourceMap = CreateSourceMap(contentSpan, context.LineIndex)
                };

                var paragraph = new MarkdownParagraph(contentSpan, new MarkdownInline[] { text })
                {
                    SourceMap = CreateSourceMap(contentSpan, context.LineIndex)
                };
                quoteBlocks.Add(paragraph);
            }

            end = token.Span.Start + token.Span.Length;
            index++;
        }

        var span = TextSpan.FromBounds(start, end);
        return new MarkdownBlockQuote(span, depth, quoteBlocks.ToArray())
        {
            SourceMap = CreateSourceMap(span, context.LineIndex)
        };
    }

    private static MarkdownBlock ParseTable(in BlockParsingContext context, MarkdownToken[] tokens, ref int index)
    {
        var source = context.Source.Span;
        var rows = new List<MarkdownToken>();
        var startIndex = index;

        while (index < tokens.Length && tokens[index].Kind == MarkdownTokenKind.Table)
        {
            rows.Add(tokens[index]);
            index++;
        }

        if (rows.Count < 2)
        {
            var fallbackToken = rows[0];
            var textSpan = TrimTrailingNewline(source[fallbackToken.Span.Start..(fallbackToken.Span.Start + fallbackToken.Span.Length)]);
            var span = new TextSpan(fallbackToken.Span.Start, textSpan.Length);
            var text = new MarkdownText(span)
            {
                SourceMap = CreateSourceMap(span, context.LineIndex)
            };

            return new MarkdownParagraph(span, new MarkdownInline[] { text })
            {
                SourceMap = CreateSourceMap(span, context.LineIndex)
            };
        }

        var headerToken = rows[0];
        var alignmentToken = rows[1];
        var alignments = TryParseTableAlignment(context, alignmentToken.Span, out var parsedAlignments)
            ? parsedAlignments
            : Array.Empty<MarkdownTableAlignment>();

        using var tableRows = new PooledBuffer<MarkdownTableRow>();
        var headerCells = ParseTableCells(context, headerToken.Span);
        tableRows.Add(new MarkdownTableRow(headerToken.Span, headerCells, true)
        {
            SourceMap = CreateSourceMap(headerToken.Span, context.LineIndex)
        });

        for (var i = 2; i < rows.Count; i++)
        {
            var rowToken = rows[i];
            var cells = ParseTableCells(context, rowToken.Span);
            tableRows.Add(new MarkdownTableRow(rowToken.Span, cells, false)
            {
                SourceMap = CreateSourceMap(rowToken.Span, context.LineIndex)
            });
        }

        if (alignments.Length == 0)
        {
            alignments = new MarkdownTableAlignment[tableRows[0].Cells.Count];
        }
        else if (alignments.Length < tableRows[0].Cells.Count)
        {
            Array.Resize(ref alignments, tableRows[0].Cells.Count);
        }

        var spanStart = rows[0].Span.Start;
        var spanEnd = rows[^1].Span.Start + rows[^1].Span.Length;
        var tableSpan = TextSpan.FromBounds(spanStart, spanEnd);

        return new MarkdownTable(tableSpan, tableRows.ToArray(), alignments)
        {
            SourceMap = CreateSourceMap(tableSpan, context.LineIndex)
        };
    }

    private static MarkdownHtmlBlock ParseHtmlBlock(in BlockParsingContext context, MarkdownToken token)
    {
        return new MarkdownHtmlBlock(token.Span)
        {
            SourceMap = CreateSourceMap(token.Span, context.LineIndex)
        };
    }

    private static MarkdownCustomContainer ParseCustomContainer(in BlockParsingContext context, MarkdownToken token)
    {
        var source = context.Source.Span;
        var slice = source[token.Span.Start..(token.Span.Start + token.Span.Length)];
        var firstLineLength = GetLineLength(slice);
        var firstLine = slice[..firstLineLength];
        var infoSlice = firstLine.Length > 3 ? firstLine[3..].Trim() : ReadOnlySpan<char>.Empty;
        var info = infoSlice.Length > 0 ? new string(infoSlice) : string.Empty;

        var offset = firstLineLength;
        if (offset < slice.Length && slice[offset] == '\r')
        {
            offset++;
        }

        if (offset < slice.Length && slice[offset] == '\n')
        {
            offset++;
        }

        var closingOffset = FindClosingCustomContainerOffset(slice, offset);
        var contentStart = token.Span.Start + offset;
        var contentLength = Math.Max(0, closingOffset - offset);
        MarkdownBlock[] children;
        if (contentLength > 0)
        {
            var (line, _) = context.LineIndex.GetLineAndColumn(contentStart);
            children = ParseSegment(context, contentStart, contentLength, Math.Max(0, line - 1));
        }
        else
        {
            children = Array.Empty<MarkdownBlock>();
        }

        return new MarkdownCustomContainer(token.Span, info, children)
        {
            SourceMap = CreateSourceMap(token.Span, context.LineIndex)
        };
    }

    private static MarkdownSourceMap CreateSourceMap(TextSpan span, SourceLineIndex lineIndex)
    {
        if (span.Length <= 0)
        {
            var (line, column) = lineIndex.GetLineAndColumn(span.Start);
            return new MarkdownSourceMap(line, column, line, column);
        }

        var (startLine, startColumn) = lineIndex.GetLineAndColumn(span.Start);
        var (endLine, endColumn) = lineIndex.GetLineAndColumn(span.Start + span.Length - 1);
        return new MarkdownSourceMap(startLine, startColumn, endLine, endColumn);
    }

    private static (TextSpan Span, MarkdownTaskState TaskState) ExtractListItemContent(ReadOnlySpan<char> source, MarkdownToken token, bool isOrdered)
    {
        var slice = TrimTrailingNewline(source[token.Span.Start..(token.Span.Start + token.Span.Length)]);
        var index = 0;

        while (index < slice.Length && char.IsWhiteSpace(slice[index]))
        {
            index++;
        }

        if (isOrdered)
        {
            while (index < slice.Length && char.IsDigit(slice[index]))
            {
                index++;
            }

            if (index < slice.Length && (slice[index] is '.' or ')'))
            {
                index++;
            }
        }
        else if (index < slice.Length && slice[index] is '-' or '*' or '+')
        {
            index++;
        }

        while (index < slice.Length && slice[index] is ' ' or '\t')
        {
            index++;
        }

        var taskState = MarkdownTaskState.None;
        if (index + 2 < slice.Length && slice[index] == '[' && slice[index + 2] == ']')
        {
            var marker = char.ToLowerInvariant(slice[index + 1]);
            taskState = marker == 'x' ? MarkdownTaskState.Complete : MarkdownTaskState.Incomplete;
            index += 3;
            if (index < slice.Length && slice[index] == ' ')
            {
                index++;
            }
        }

        var trimmed = slice[index..].TrimStart();
        var offset = slice[index..].Length - trimmed.Length;
        var start = token.Span.Start + index + offset;
        return (new TextSpan(start, trimmed.Length), taskState);
    }

    private static MarkdownTaskState GetTaskStateFromToken(int value)
    {
        MarkdownTokenizer.DecodeOrderedListValue(value, out _, out var taskState);
        return taskState;
    }

    private static TextSpan ExtractBlockQuoteContentSpan(ReadOnlySpan<char> source, MarkdownToken token)
    {
        var slice = TrimTrailingNewline(source[token.Span.Start..(token.Span.Start + token.Span.Length)]);
        var index = 0;

        while (index < slice.Length && slice[index] == '>')
        {
            index++;
            if (index < slice.Length && slice[index] == ' ')
            {
                index++;
            }
        }

        var trimmed = slice[index..].TrimStart();
        var offset = slice[index..].Length - trimmed.Length;
        var start = token.Span.Start + index + offset;
        return new TextSpan(start, trimmed.Length);
    }

    private static bool TryParseTableAlignment(in BlockParsingContext context, TextSpan span, out MarkdownTableAlignment[] alignments)
    {
        var slice = context.Source.Span[span.Start..(span.Start + span.Length)];
        var trimmed = slice.Trim();
        if (trimmed.IsEmpty)
        {
            alignments = Array.Empty<MarkdownTableAlignment>();
            return false;
        }

        using var list = new PooledBuffer<MarkdownTableAlignment>();
        var segments = EnumerateTableSegments(slice);
        foreach (var segment in segments)
        {
            var cellSlice = segment.Length > 0 ? slice.Slice(segment.StartOffset, segment.Length) : ReadOnlySpan<char>.Empty;
            if (!TryParseAlignment(cellSlice, out var alignment))
            {
                alignments = Array.Empty<MarkdownTableAlignment>();
                return false;
            }

            list.Add(alignment);
        }

        alignments = list.ToArray();
        return true;
    }

    private static IReadOnlyList<MarkdownTableCell> ParseTableCells(in BlockParsingContext context, TextSpan rowSpan)
    {
        var source = context.Source.Span;
        var rowSlice = source[rowSpan.Start..(rowSpan.Start + rowSpan.Length)];
        var segments = EnumerateTableSegments(rowSlice);
        var cells = new List<MarkdownTableCell>(segments.Count);

        foreach (var segment in segments)
        {
            var cellSlice = segment.Length > 0 ? rowSlice.Slice(segment.StartOffset, segment.Length) : ReadOnlySpan<char>.Empty;
            var trimmed = cellSlice.Trim();
            var leadingWhitespace = cellSlice.Length - cellSlice.TrimStart().Length;
            var start = rowSpan.Start + segment.StartOffset + leadingWhitespace;
            var cellSpan = new TextSpan(start, trimmed.Length);
            cells.Add(new MarkdownTableCell(cellSpan)
            {
                SourceMap = CreateSourceMap(cellSpan, context.LineIndex)
            });
        }

        return cells;
    }

    private static IReadOnlyList<(int StartOffset, int Length)> EnumerateTableSegments(ReadOnlySpan<char> row)
    {
        var segments = new List<(int StartOffset, int Length)>();
        var start = 0;
        var index = 0;

        if (row.Length > 0 && row[0] == '|')
        {
            start = 1;
        }

        for (index = start; index <= row.Length; index++)
        {
            if (index == row.Length || row[index] == '|')
            {
                var length = Math.Max(0, index - start);
                segments.Add((start, length));
                start = index + 1;
            }
        }

        return segments;
    }

    private static bool TryParseAlignment(ReadOnlySpan<char> cell, out MarkdownTableAlignment alignment)
    {
        var trimmed = cell.Trim();
        if (trimmed.IsEmpty)
        {
            alignment = MarkdownTableAlignment.Unspecified;
            return true;
        }

        var leadingColon = trimmed.Length > 0 && trimmed[0] == ':';
        var trailingColon = trimmed.Length > 0 && trimmed[^1] == ':';
        var core = trimmed;

        if (leadingColon)
        {
            core = core[1..];
        }

        if (trailingColon && core.Length > 0)
        {
            core = core[..^1];
        }

        if (core.Length == 0)
        {
            alignment = MarkdownTableAlignment.Unspecified;
            return false;
        }

        for (var i = 0; i < core.Length; i++)
        {
            if (core[i] != '-')
            {
                alignment = MarkdownTableAlignment.Unspecified;
                return false;
            }
        }

        alignment = leadingColon && trailingColon
            ? MarkdownTableAlignment.Center
            : leadingColon
                ? MarkdownTableAlignment.Left
                : trailingColon
                    ? MarkdownTableAlignment.Right
                    : MarkdownTableAlignment.Unspecified;
        return true;
    }

    private static ReadOnlySpan<char> TrimTrailingNewline(ReadOnlySpan<char> span)
    {
        var end = span.Length;
        while (end > 0 && (span[end - 1] == '\n' || span[end - 1] == '\r'))
        {
            end--;
        }

        return span[..end];
    }

    private static int GetLineLength(ReadOnlySpan<char> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] == '\r' || span[i] == '\n')
            {
                return i;
            }
        }

        return span.Length;
    }

    private static int FindClosingFence(ReadOnlySpan<char> span, char marker)
    {
        for (var i = span.Length - 1; i >= 0; i--)
        {
            if (span[i] == marker)
            {
                var count = 1;
                var j = i - 1;
                while (j >= 0 && span[j] == marker)
                {
                    count++;
                    j--;
                }

                if (count >= 3)
                {
                    return j + 1;
                }
            }
        }

        return span.Length;
    }

    private static int FindClosingCustomContainerOffset(ReadOnlySpan<char> span, int start)
    {
        var offset = start;
        while (offset < span.Length)
        {
            var remaining = span[offset..];
            var lineLength = GetLineLength(remaining);
            var trimmed = remaining[..lineLength].Trim();
            if (trimmed.StartsWith(":::", StringComparison.Ordinal))
            {
                return offset;
            }

            offset += lineLength;
            if (offset < span.Length && span[offset] == '\r')
            {
                offset++;
            }

            if (offset < span.Length && span[offset] == '\n')
            {
                offset++;
            }
        }

        return span.Length;
    }

    private static MarkdownToken ApplyTokenFilters(ReadOnlySpan<char> source, IReadOnlyList<IMarkdownTokenFilter> filters, MarkdownToken token)
    {
        if (filters is null || filters.Count == 0)
        {
            return token;
        }

        var current = token;
        for (var i = 0; i < filters.Count; i++)
        {
            current = filters[i].Filter(source, current);
            if (current.Kind == MarkdownTokenKind.None)
            {
                break;
            }
        }

        return current;
    }

    private static void ApplyDocumentTransformers(IReadOnlyList<IMarkdownDocumentTransformer> transformers, MarkdownDocument document)
    {
        if (transformers is null)
        {
            return;
        }

        for (var i = 0; i < transformers.Count; i++)
        {
            transformers[i].Transform(document);
        }
    }

    private readonly record struct BlockParsingContext(
        ReadOnlyMemory<char> Source,
        MarkdownOptions Options,
        IReadOnlyList<IMarkdownTokenFilter> TokenFilters,
        SourceLineIndex LineIndex);
}
