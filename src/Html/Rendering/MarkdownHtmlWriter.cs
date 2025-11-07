using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using SkiaMarkdown.Syntax;
using SkiaMarkdown.Syntax.Red;

namespace SkiaMarkdown.Html;

/// <summary>
/// Traverses the Roslyn-style Markdown syntax tree and emits HTML.
/// </summary>
internal sealed class MarkdownHtmlWriter : MarkdownSyntaxVisitor
{
    private static readonly string[] HeadingTags = { "h1", "h2", "h3", "h4", "h5", "h6" };
    private static readonly string[] DefaultDisallowedHtmlTags =
    {
        "title",
        "textarea",
        "style",
        "xmp",
        "iframe",
        "noembed",
        "noframes",
        "script",
        "plaintext"
    };

    private readonly TextWriter _writer;
    private readonly MarkdownHtmlOptions _options;
    private readonly HashSet<string> _disallowedHtmlTags;
    private readonly Dictionary<string, MarkdownSyntaxNode> _footnoteDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _footnoteOrder = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _footnoteReferenceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _footnoteOrderList = new();
    private bool _footnotesWritten;

    public MarkdownHtmlWriter(TextWriter writer, MarkdownHtmlOptions options)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        var tagSource = _options.DisallowedRawHtmlTags ?? DefaultDisallowedHtmlTags;
        _disallowedHtmlTags = new HashSet<string>(tagSource, StringComparer.OrdinalIgnoreCase);
    }

    public override void Visit(MarkdownSyntaxNode node)
    {
        switch (node.Kind)
        {
            case MarkdownSyntaxKind.MarkdownDocument:
            case MarkdownSyntaxKind.SyntaxList:
                foreach (var child in node.ChildNodes())
                {
                    Visit(child);
                }

                break;
            case MarkdownSyntaxKind.ParagraphBlock:
                WriteParagraph(node);
                break;
            case MarkdownSyntaxKind.AtxHeadingBlock:
                WriteAtxHeading(node);
                break;
            case MarkdownSyntaxKind.ThematicBreakBlock:
                _writer.WriteLine("<hr />");
                break;
            case MarkdownSyntaxKind.BlockQuoteBlock:
                WriteBlockQuote(node);
                break;
            case MarkdownSyntaxKind.BulletListBlock:
                WriteList(node, ordered: false);
                break;
            case MarkdownSyntaxKind.OrderedListBlock:
                WriteList(node, ordered: true);
                break;
            case MarkdownSyntaxKind.ListItemBlock:
                WriteListItem(node);
                break;
            case MarkdownSyntaxKind.FencedCodeBlock:
                WriteFencedCode(node);
                break;
            case MarkdownSyntaxKind.HtmlBlock:
                WriteHtmlBlock(node);
                break;
            case MarkdownSyntaxKind.CustomContainerBlock:
                WriteCustomContainer(node);
                break;
            case MarkdownSyntaxKind.TableBlock:
                WriteTable(node);
                break;
            case MarkdownSyntaxKind.FootnoteDefinitionBlock:
                RegisterFootnoteDefinition(node);
                break;
            default:
                if (MarkdownSyntaxFacts.IsInlineNode(node.Kind))
                {
                    WriteInlineNode(node);
                }
                else
                {
                    base.Visit(node);
                }

                break;
        }
    }

    public override void VisitToken(MarkdownSyntaxToken token)
    {
        // Tokens are emitted explicitly through inline handling.
    }

    private void WriteParagraph(MarkdownSyntaxNode node, bool wrap = true)
    {
        if (!HasInlineContent(node))
        {
            return;
        }

        if (TryWriteSetextHeading(node))
        {
            return;
        }

        if (TryWriteIndentedCode(node))
        {
            return;
        }

        if (wrap)
        {
            _writer.Write("<p>");
        }

        var inlineList = node.ChildNodes().FirstOrDefault(child => child.Kind == MarkdownSyntaxKind.SyntaxList);
        if (inlineList is not null)
        {
            var skipBreaks = CountTrailingLineBreaks(inlineList);
            WriteInlineChildren(inlineList, skipBreaks);
        }
        else
        {
            WriteInlineChildren(node);
        }

        if (wrap)
        {
            _writer.WriteLine("</p>");
        }
        else
        {
            _writer.WriteLine();
        }
    }

    private void WriteAtxHeading(MarkdownSyntaxNode node)
    {
        var level = GetHeadingLevel(node);
        var tag = HeadingTags[Math.Clamp(level - 1, 0, HeadingTags.Length - 1)];

        _writer.Write('<');
        _writer.Write(tag);
        _writer.Write('>');

        var textNode = FindChild(node, MarkdownSyntaxKind.HeadingText);
        if (textNode is not null)
        {
            WriteInlineChildren(textNode);
        }

        _writer.Write("</");
        _writer.Write(tag);
        _writer.WriteLine(">");
    }

    private void WriteBlockQuote(MarkdownSyntaxNode node)
    {
        _writer.WriteLine("<blockquote>");
        foreach (var child in node.ChildNodes())
        {
            if (child.Kind == MarkdownSyntaxKind.BlockQuotePrefix)
            {
                continue;
            }

            Visit(child);
        }

        _writer.WriteLine("</blockquote>");
    }

    private void WriteList(MarkdownSyntaxNode node, bool ordered)
    {
        _writer.WriteLine(ordered ? "<ol>" : "<ul>");

        foreach (var child in node.ChildNodes())
        {
            Visit(child);
        }

        _writer.WriteLine(ordered ? "</ol>" : "</ul>");
    }

    private void WriteListItem(MarkdownSyntaxNode node)
    {
        var children = node.ChildNodes().ToList();
        var taskState = children.FirstOrDefault(child => child.Kind == MarkdownSyntaxKind.TaskListState);
        var contentChildren = children.Where(child => child.Kind != MarkdownSyntaxKind.TaskListState).ToList();
        var isTight = contentChildren.Count > 0 && contentChildren.All(child => child.Kind == MarkdownSyntaxKind.ParagraphBlock);

        _writer.Write("<li>");

        if (taskState is not null)
        {
            var isChecked = ParseTaskListState(taskState);
            _writer.Write("<input type=\"checkbox\" disabled");
            if (isChecked)
            {
                _writer.Write(" checked");
            }

            _writer.Write(" /> ");
        }

        foreach (var child in contentChildren)
        {
            if (isTight && child.Kind == MarkdownSyntaxKind.ParagraphBlock)
            {
                WriteParagraph(child, wrap: false);
            }
            else
            {
                Visit(child);
            }
        }

        _writer.WriteLine("</li>");
    }

    private void WriteFencedCode(MarkdownSyntaxNode node)
    {
        var openingFence = FindChild(node, MarkdownSyntaxKind.FencedCodeFence);
        var info = ExtractInfoString(openingFence);
        var language = string.IsNullOrWhiteSpace(info) ? null : info.Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

        _writer.Write("<pre><code");
        if (!string.IsNullOrWhiteSpace(language))
        {
            _writer.Write(" class=\"language-");
            WriteAttribute(language);
            _writer.Write('"');
        }

        _writer.Write('>');

        var textNode = FindChild(node, MarkdownSyntaxKind.FencedCodeText);
        if (textNode is not null)
        {
            foreach (var token in textNode.ChildTokens())
            {
                WriteEscaped(token.Text);
            }
        }

        _writer.WriteLine("</code></pre>");
    }

    private void WriteHtmlBlock(MarkdownSyntaxNode node)
    {
        foreach (var token in node.ChildTokens())
        {
            var text = token.Text ?? string.Empty;
            if (_options.AllowRawHtml)
            {
                _writer.WriteLine(ApplyRawHtmlFilters(text));
            }
            else
            {
                WriteEscaped(text);
                if (!EndsWithLineEnding(text))
                {
                    _writer.WriteLine();
                }
            }
        }
    }

    private void WriteCustomContainer(MarkdownSyntaxNode node)
    {
        var token = node.ChildTokens().FirstOrDefault();
        if (token is null || string.IsNullOrEmpty(token.Text))
        {
            return;
        }

        if (!TryParseCustomContainer(token.Text, out var parts))
        {
            WriteEscaped(token.Text);
            return;
        }

        var classValue = BuildCustomContainerClass(parts);
        _writer.Write("<div class=\"");
        WriteAttribute(classValue);
        _writer.Write('"');

        if (!string.IsNullOrEmpty(parts.Kind))
        {
            _writer.Write(" data-container-kind=\"");
            WriteAttribute(parts.Kind);
            _writer.Write('"');
        }

        if (!string.IsNullOrEmpty(parts.Identifier))
        {
            _writer.Write(" data-container-id=\"");
            WriteAttribute(parts.Identifier);
            _writer.Write('"');
        }

        if (!string.IsNullOrEmpty(parts.RawInfo))
        {
            _writer.Write(" data-container-info=\"");
            WriteAttribute(parts.RawInfo);
            _writer.Write('"');
        }

        foreach (var kvp in parts.Metadata)
        {
            if (string.IsNullOrEmpty(kvp.Key))
            {
                continue;
            }

            var normalizedKey = NormalizeDataAttributeKey(kvp.Key);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                continue;
            }

            _writer.Write(" data-");
            _writer.Write(normalizedKey);
            _writer.Write("=\"");
            WriteAttribute(kvp.Value);
            _writer.Write('"');
        }

        _writer.Write('>');
        _writer.WriteLine();

        var innerContent = parts.Content.Trim();
        if (!string.IsNullOrEmpty(innerContent))
        {
            var innerTree = MarkdownSyntaxTree.Parse(innerContent, MarkdownHtmlGenerator.SyntaxOptions);
            foreach (var child in innerTree.GetRoot().ChildNodes())
            {
                Visit(child);
            }
        }

        _writer.WriteLine("</div>");
    }

    private void WriteTable(MarkdownSyntaxNode node)
    {
        var alignments = GetTableAlignments(node);

        _writer.WriteLine("<table>");

        var header = FindChild(node, MarkdownSyntaxKind.TableHeader);
        if (header is not null)
        {
            _writer.WriteLine("<thead>");
            WriteTableRow(header, alignments, isHeader: true);
            _writer.WriteLine("</thead>");
        }

        var body = FindChild(node, MarkdownSyntaxKind.TableBody);
        if (body is not null)
        {
            _writer.WriteLine("<tbody>");
            foreach (var row in EnumerateChildren(body, MarkdownSyntaxKind.TableRow))
            {
                WriteTableRow(row, alignments, isHeader: false);
            }

            _writer.WriteLine("</tbody>");
        }

        _writer.WriteLine("</table>");
    }

    private void WriteTableRow(MarkdownSyntaxNode node, string?[] alignments, bool isHeader)
    {
        var cells = EnumerateChildren(node, MarkdownSyntaxKind.TableCell).ToList();
        if (cells.Count == 0 && node.Kind == MarkdownSyntaxKind.TableHeader)
        {
            cells = EnumerateChildren(node, MarkdownSyntaxKind.SyntaxList)
                .SelectMany(child => EnumerateChildren(child, MarkdownSyntaxKind.TableCell))
                .ToList();
        }

        if (cells.Count == 0)
        {
            return;
        }

        _writer.Write("<tr>");
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var tag = isHeader ? "th" : "td";
            _writer.Write('<');
            _writer.Write(tag);

            var alignment = i < alignments.Length ? alignments[i] : null;
            if (!string.IsNullOrEmpty(alignment))
            {
                _writer.Write(" style=\"text-align:");
                _writer.Write(alignment);
                _writer.Write('"');
            }

            _writer.Write('>');
            WriteInlineChildren(cell);
            _writer.Write("</");
            _writer.Write(tag);
            _writer.Write('>');
        }

        _writer.WriteLine("</tr>");
    }

    private void WriteInlineNode(MarkdownSyntaxNode node)
    {
        switch (node.Kind)
        {
            case MarkdownSyntaxKind.SyntaxList:
                WriteInlineChildren(node);
                break;
            case MarkdownSyntaxKind.TextInline:
                WriteInlineChildren(node);
                break;
            case MarkdownSyntaxKind.EmphasisInline:
                _writer.Write("<em>");
                WriteInlineChildren(node);
                _writer.Write("</em>");
                break;
            case MarkdownSyntaxKind.StrongEmphasisInline:
                _writer.Write("<strong>");
                WriteInlineChildren(node);
                _writer.Write("</strong>");
                break;
            case MarkdownSyntaxKind.StrikethroughInline:
                _writer.Write("<del>");
                WriteInlineChildren(node);
                _writer.Write("</del>");
                break;
            case MarkdownSyntaxKind.HighlightInline:
                _writer.Write("<mark>");
                WriteInlineChildren(node);
                _writer.Write("</mark>");
                break;
            case MarkdownSyntaxKind.CodeSpanInline:
                _writer.Write("<code>");
                WriteInlineChildren(node);
                _writer.Write("</code>");
                break;
            case MarkdownSyntaxKind.MathInline:
                WriteMathInline(node);
                break;
            case MarkdownSyntaxKind.EntityInline:
                WriteEntityInline(node);
                break;
            case MarkdownSyntaxKind.MentionInline:
                WriteMentionInline(node);
                break;
            case MarkdownSyntaxKind.EmojiInline:
                WriteEmojiInline(node);
                break;
            case MarkdownSyntaxKind.SoftBreakInline:
                _writer.Write(_options.SoftBreak);
                break;
            case MarkdownSyntaxKind.HardBreakInline:
            case MarkdownSyntaxKind.LineBreakInline:
                _writer.Write("<br />");
                _writer.WriteLine();
                break;
            case MarkdownSyntaxKind.LinkInline:
                WriteLinkInline(node);
                break;
            case MarkdownSyntaxKind.ImageInline:
                WriteImageInline(node);
                break;
            case MarkdownSyntaxKind.AutolinkInline:
                WriteAutolinkInline(node);
                break;
            case MarkdownSyntaxKind.HtmlInline:
                WriteRawHtmlInline(node);
                break;
            case MarkdownSyntaxKind.FootnoteReferenceInline:
                WriteFootnoteReference(node);
                break;
            case MarkdownSyntaxKind.TaskListState:
                // Task list markers are rendered in list item context.
                break;
            default:
                WriteInlineChildren(node);
                break;
        }
    }

    private void WriteInlineChildren(MarkdownSyntaxNode node, int skipFromEnd = 0)
    {
        var elements = node.ChildNodesAndTokens().ToList();
        var limit = Math.Max(0, elements.Count - skipFromEnd);

        for (var i = 0; i < limit; i++)
        {
            var element = elements[i];
            if (element.IsNode)
            {
                var child = element.AsNode();
                if (child.Kind == MarkdownSyntaxKind.SyntaxList || MarkdownSyntaxFacts.IsInlineNode(child.Kind))
                {
                    WriteInlineNode(child);
                }
                else
                {
                    Visit(child);
                }
            }
            else
            {
                WriteInlineToken(element.AsToken());
            }
        }
    }

    private void WriteLinkInline(MarkdownSyntaxNode node)
    {
        var destinationToken = GetDestinationToken(node);
        var (href, title) = ParseLinkDestination(destinationToken?.Text);

        _writer.Write("<a href=\"");
        WriteAttribute(href);
        _writer.Write('"');

        if (!string.IsNullOrEmpty(title))
        {
            _writer.Write(" title=\"");
            WriteAttribute(title!);
            _writer.Write('"');
        }

        _writer.Write('>');

        var labelNode = node.ChildNodes().FirstOrDefault();
        if (labelNode is not null)
        {
            WriteInlineChildren(labelNode);
        }

        _writer.Write("</a>");
    }

    private void WriteImageInline(MarkdownSyntaxNode node)
    {
        var destinationToken = GetDestinationToken(node);
        var (src, title) = ParseLinkDestination(destinationToken?.Text);
        var labelNode = node.ChildNodes().FirstOrDefault();
        var altText = labelNode is null ? string.Empty : CollectPlainText(labelNode);

        _writer.Write("<img src=\"");
        WriteAttribute(src);
        _writer.Write("\" alt=\"");
        WriteAttribute(altText);
        _writer.Write('"');

        if (!string.IsNullOrEmpty(title))
        {
            _writer.Write(" title=\"");
            WriteAttribute(title!);
            _writer.Write('"');
        }

        _writer.Write(" />");
    }

    private void WriteAutolinkInline(MarkdownSyntaxNode node)
    {
        var token = GetDestinationToken(node);
        if (token is null)
        {
            return;
        }

        var text = token.Text?.Trim() ?? string.Empty;
        if (text.Length >= 2 && text[0] == '<' && text[^1] == '>')
        {
            text = text[1..^1];
        }

        _writer.Write("<a href=\"");
        WriteAttribute(text);
        _writer.Write("\">");
        WriteEscaped(text);
        _writer.Write("</a>");
    }

    private void WriteMentionInline(MarkdownSyntaxNode node)
    {
        var text = GetFirstTokenText(node);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var identifier = text.StartsWith("@", StringComparison.Ordinal) && text.Length > 1
            ? text[1..]
            : text;

        _writer.Write("<span class=\"mention\"");
        if (!string.IsNullOrEmpty(identifier))
        {
            _writer.Write(" data-mention=\"");
            WriteAttribute(identifier);
            _writer.Write('"');
        }

        _writer.Write('>');
        WriteEscaped(text);
        _writer.Write("</span>");
    }

    private void WriteEmojiInline(MarkdownSyntaxNode node)
    {
        var text = GetFirstTokenText(node);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var shortcode = text.Trim(':');
        var resolved = !string.IsNullOrEmpty(shortcode)
            ? _options.EmojiResolver?.Invoke(shortcode)
            : null;
        var display = string.IsNullOrEmpty(resolved) ? text : resolved!;

        _writer.Write("<span class=\"emoji\"");
        if (!string.IsNullOrEmpty(shortcode))
        {
            _writer.Write(" data-emoji=\"");
            WriteAttribute(shortcode);
            _writer.Write('"');
        }

        _writer.Write('>');
        WriteEscaped(display);
        _writer.Write("</span>");
    }

    private void WriteMathInline(MarkdownSyntaxNode node)
    {
        var content = CollectPlainText(node);
        _writer.Write("<span class=\"math math-inline\"");
        if (!string.IsNullOrEmpty(content))
        {
            _writer.Write(" data-math=\"");
            WriteAttribute(content);
            _writer.Write('"');
        }

        _writer.Write('>');
        WriteEscaped(content);
        _writer.Write("</span>");
    }

    private void WriteEntityInline(MarkdownSyntaxNode node)
    {
        var token = node.ChildTokens().FirstOrDefault();
        if (token is null || string.IsNullOrEmpty(token.Text))
        {
            return;
        }

        var decoded = WebUtility.HtmlDecode(token.Text);
        WriteEscaped(decoded);
    }

    private void WriteRawHtmlInline(MarkdownSyntaxNode node)
    {
        foreach (var token in node.ChildTokens())
        {
            var text = token.Text ?? string.Empty;
            if (_options.AllowRawHtml)
            {
                _writer.Write(ApplyRawHtmlFilters(text));
            }
            else
            {
                WriteEscaped(text);
            }
        }
    }

    private string ApplyRawHtmlFilters(string text)
    {
        if (string.IsNullOrEmpty(text) || !_options.FilterDisallowedRawHtml || _disallowedHtmlTags.Count == 0)
        {
            return text;
        }

        return FilterDisallowedRawHtml(text);
    }

    private string FilterDisallowedRawHtml(string text)
    {
        var span = text.AsSpan();
        StringBuilder? builder = null;
        var lastCopyIndex = 0;

        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] == '<' && IsDisallowedHtmlTag(span, i + 1))
            {
                builder ??= new StringBuilder(span.Length + 16);
                if (i > lastCopyIndex)
                {
                    builder.Append(span.Slice(lastCopyIndex, i - lastCopyIndex));
                }

                builder.Append("&lt;");
                lastCopyIndex = i + 1;
            }
        }

        if (builder is null)
        {
            return text;
        }

        if (lastCopyIndex < span.Length)
        {
            builder.Append(span[lastCopyIndex..]);
        }

        return builder.ToString();
    }

    private bool IsDisallowedHtmlTag(ReadOnlySpan<char> text, int position)
    {
        if (position >= text.Length)
        {
            return false;
        }

        if (text[position] == '!' || text[position] == '?')
        {
            return false;
        }

        var index = position;
        if (text[index] == '/')
        {
            index++;
        }

        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        var nameStart = index;
        while (index < text.Length && char.IsLetter(text[index]))
        {
            index++;
        }

        if (nameStart == index)
        {
            return false;
        }

        var name = text[nameStart..index].ToString();
        if (!_disallowedHtmlTags.Contains(name))
        {
            return false;
        }

        if (index >= text.Length)
        {
            return true;
        }

        var terminator = text[index];
        return char.IsWhiteSpace(terminator) || terminator == '>' || terminator == '/' || terminator == '\0';
    }

    private void WriteInlineToken(MarkdownSyntaxToken token)
    {
        if (string.IsNullOrEmpty(token.Text))
        {
            return;
        }

        WriteEscaped(token.Text);
    }

    private void WriteEscaped(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var ch in text)
        {
            switch (ch)
            {
                case '<':
                    _writer.Write("&lt;");
                    break;
                case '>':
                    _writer.Write("&gt;");
                    break;
                case '&':
                    _writer.Write("&amp;");
                    break;
                case '"':
                    _writer.Write("&quot;");
                    break;
                case '\'':
                    _writer.Write("&#39;");
                    break;
                default:
                    _writer.Write(ch);
                    break;
            }
        }
    }

    private void WriteAttribute(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var ch in text)
        {
            switch (ch)
            {
                case '"':
                    _writer.Write("&quot;");
                    break;
                case '&':
                    _writer.Write("&amp;");
                    break;
                case '<':
                    _writer.Write("&lt;");
                    break;
                case '>':
                    _writer.Write("&gt;");
                    break;
                default:
                    _writer.Write(ch);
                    break;
            }
        }
    }

    private static MarkdownSyntaxNode? FindChild(MarkdownSyntaxNode node, MarkdownSyntaxKind kind)
    {
        foreach (var child in node.ChildNodes())
        {
            if (child.Kind == kind)
            {
                return child;
            }
        }

        return null;
    }

    private static int CountTrailingLineBreaks(MarkdownSyntaxNode node)
    {
        var elements = node.ChildNodesAndTokens().ToList();
        var count = 0;

        for (var i = elements.Count - 1; i >= 0; i--)
        {
            if (!elements[i].IsNode)
            {
                break;
            }

            var kind = elements[i].AsNode().Kind;
            if (kind == MarkdownSyntaxKind.SoftBreakInline ||
                kind == MarkdownSyntaxKind.HardBreakInline ||
                kind == MarkdownSyntaxKind.LineBreakInline)
            {
                count++;
                continue;
            }

            break;
        }

        return count;
    }

    private static bool HasInlineContent(MarkdownSyntaxNode node)
    {
        foreach (var element in node.ChildNodesAndTokens())
        {
            if (element.IsNode)
            {
                var child = element.AsNode();
                if (child.Kind == MarkdownSyntaxKind.SyntaxList && HasInlineContent(child))
                {
                    return true;
                }

                if (MarkdownSyntaxFacts.IsInlineNode(child.Kind))
                {
                    return true;
                }
            }
            else if (!string.IsNullOrEmpty(element.AsToken().Text))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryWriteSetextHeading(MarkdownSyntaxNode paragraph)
    {
        var list = paragraph.ChildNodes().FirstOrDefault();
        if (list is null || list.Kind != MarkdownSyntaxKind.SyntaxList)
        {
            return false;
        }

        var children = list.ChildNodes().ToList();
        if (children.Count == 0)
        {
            return false;
        }

        var skipCount = 0;
        char? delimiter = null;
        var index = children.Count - 1;

        while (index >= 0)
        {
            var child = children[index];
            if (child.Kind == MarkdownSyntaxKind.TextInline)
            {
                var text = CollectPlainText(child);
                if (string.IsNullOrWhiteSpace(text))
                {
                    skipCount++;
                    index--;
                    continue;
                }

                var trimmed = text.Trim();
                if (trimmed.All(ch => ch == '=') || trimmed.All(ch => ch == '-'))
                {
                    var currentDelimiter = trimmed[0];
                    if (delimiter is null)
                    {
                        delimiter = currentDelimiter;
                    }
                    else if (delimiter != currentDelimiter)
                    {
                        return false;
                    }

                    skipCount++;
                    index--;
                    continue;
                }

                return false;
            }

            if (child.Kind == MarkdownSyntaxKind.SoftBreakInline || child.Kind == MarkdownSyntaxKind.HardBreakInline)
            {
                if (delimiter is null)
                {
                    return false;
                }

                skipCount++;
                index--;
                break;
            }

            if (child.Kind == MarkdownSyntaxKind.SyntaxList && !HasInlineContent(child))
            {
                skipCount++;
                index--;
                continue;
            }

            return false;
        }

        if (delimiter is null)
        {
            return false;
        }

        var level = delimiter == '=' ? 1 : 2;
        var tag = HeadingTags[Math.Clamp(level - 1, 0, HeadingTags.Length - 1)];

        _writer.Write('<');
        _writer.Write(tag);
        _writer.Write('>');
        WriteInlineChildren(list, skipCount);
        _writer.Write("</");
        _writer.Write(tag);
        _writer.WriteLine(">");
        return true;
    }

    private bool TryWriteIndentedCode(MarkdownSyntaxNode paragraph)
    {
        var text = CollectPlainTextWithBreaks(paragraph);
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 0)
        {
            return false;
        }

        var hasContent = false;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("\t", StringComparison.Ordinal) || line.StartsWith("    ", StringComparison.Ordinal))
            {
                hasContent = true;
                continue;
            }

            return false;
        }

        if (!hasContent)
        {
            return false;
        }

        _writer.Write("<pre><code>");
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("\t", StringComparison.Ordinal))
            {
                line = line[1..];
            }
            else if (line.StartsWith("    ", StringComparison.Ordinal))
            {
                line = line[4..];
            }

            WriteEscaped(line);
            if (i < lines.Length - 1)
            {
                _writer.Write('\n');
            }
        }

        _writer.WriteLine("</code></pre>");
        return true;
    }

    private static bool ParseTaskListState(MarkdownSyntaxNode node)
    {
        foreach (var token in node.ChildTokens())
        {
            var text = token.Text?.Trim();
            if (string.Equals(text, "[x]", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetHeadingLevel(MarkdownSyntaxNode node)
    {
        var punctuation = FindChild(node, MarkdownSyntaxKind.HeadingPunctuation);
        if (punctuation is null)
        {
            return 1;
        }

        var count = 0;
        foreach (var token in punctuation.ChildTokens())
        {
            if (token.Kind == MarkdownSyntaxKind.HashToken)
            {
                count += token.Text.Length;
            }
        }

        return Math.Clamp(count, 1, 6);
    }

    private static string ExtractInfoString(MarkdownSyntaxNode? fenceNode)
    {
        if (fenceNode is null)
        {
            return string.Empty;
        }

        foreach (var token in fenceNode.ChildTokens())
        {
            var text = token.Text ?? string.Empty;
            var span = text.AsSpan();
            var index = 0;
            while (index < span.Length && (span[index] == '`' || span[index] == '~'))
            {
                index++;
            }

            while (index < span.Length && char.IsWhiteSpace(span[index]))
            {
                index++;
            }

            if (index >= span.Length)
            {
                return string.Empty;
            }

            return span[index..].ToString().Trim();
        }

        return string.Empty;
    }

    private static string?[] GetTableAlignments(MarkdownSyntaxNode table)
    {
        var delimiterRow = FindChild(table, MarkdownSyntaxKind.TableDelimiterRow);
        if (delimiterRow is null)
        {
            return Array.Empty<string?>();
        }

        var alignments = new List<string?>();
        foreach (var cell in EnumerateChildren(delimiterRow, MarkdownSyntaxKind.TableDelimiterCell))
        {
            string? alignment = null;
            foreach (var token in cell.ChildTokens())
            {
                if (token.Kind == MarkdownSyntaxKind.TableAlignmentToken)
                {
                    alignment = token.Text switch
                    {
                        "left" => "left",
                        "right" => "right",
                        "center" => "center",
                        "none" => null,
                        _ => null
                    };
                    break;
                }
            }

            alignments.Add(alignment);
        }

        return alignments.ToArray();
    }

    private static string CollectPlainText(MarkdownSyntaxNode node)
    {
        var builder = new StringBuilder();
        AppendPlainText(node, builder);
        return builder.ToString();
    }

    private static void AppendPlainText(MarkdownSyntaxNode node, StringBuilder builder)
    {
        foreach (var element in node.ChildNodesAndTokens())
        {
            if (element.IsNode)
            {
                AppendPlainText(element.AsNode(), builder);
            }
            else
            {
                builder.Append(element.AsToken().Text);
            }
        }
    }

    private static string CollectPlainTextWithBreaks(MarkdownSyntaxNode node)
    {
        var builder = new StringBuilder();
        AppendPlainTextWithBreaks(node, builder);
        return builder.ToString();
    }

    private static void AppendPlainTextWithBreaks(MarkdownSyntaxNode node, StringBuilder builder)
    {
        foreach (var element in node.ChildNodesAndTokens())
        {
            if (element.IsNode)
            {
                var child = element.AsNode();
                if (child.Kind == MarkdownSyntaxKind.SoftBreakInline ||
                    child.Kind == MarkdownSyntaxKind.HardBreakInline ||
                    child.Kind == MarkdownSyntaxKind.LineBreakInline)
                {
                    builder.Append('\n');
                }
                else
                {
                    AppendPlainTextWithBreaks(child, builder);
                }
            }
            else
            {
                builder.Append(element.AsToken().Text);
            }
        }
    }

    private static IEnumerable<MarkdownSyntaxNode> EnumerateChildren(MarkdownSyntaxNode node, MarkdownSyntaxKind kind)
    {
        foreach (var child in node.ChildNodes())
        {
            if (child.Kind == kind)
            {
                yield return child;
            }
            else
            {
                foreach (var nested in EnumerateChildren(child, kind))
                {
                    yield return nested;
                }
            }
        }
    }

    private static MarkdownSyntaxToken? GetDestinationToken(MarkdownSyntaxNode node)
    {
        foreach (var token in node.ChildTokens())
        {
            return token;
        }

        return null;
    }

    private static string? GetFirstTokenText(MarkdownSyntaxNode node) =>
        node.ChildTokens().FirstOrDefault()?.Text;

    private static bool EndsWithLineEnding(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var last = text[^1];
        return last == '\n' || last == '\r';
    }

    public void Complete()
    {
        if (_footnotesWritten)
        {
            return;
        }

        _footnotesWritten = true;
        if (_footnoteOrderList.Count == 0)
        {
            return;
        }

        _writer.WriteLine("<section class=\"footnotes\" data-footnotes>");
        _writer.WriteLine("<hr />");
        _writer.WriteLine("<ol>");

        foreach (var label in _footnoteOrderList)
        {
            var number = _footnoteOrder[label];
            var definitionId = BuildFootnoteDefinitionId(number);
            _writer.Write("<li id=\"");
            WriteAttribute(definitionId);
            _writer.Write("\">");

            if (_footnoteDefinitions.TryGetValue(label, out var definitionNode))
            {
                foreach (var child in definitionNode.ChildNodes())
                {
                    if (child.Kind == MarkdownSyntaxKind.FootnoteName)
                    {
                        continue;
                    }

                    Visit(child);
                }
            }
            else
            {
                _writer.Write("<p>");
                WriteEscaped(label);
                _writer.Write("</p>");
            }

            var backRefId = BuildFootnoteReferenceId(number, 1);
            _writer.Write(" <a href=\"#");
            WriteAttribute(backRefId);
            _writer.Write("\" class=\"footnote-backref\" aria-label=\"Back to content\">↩</a>");
            _writer.WriteLine("</li>");
        }

        _writer.WriteLine("</ol>");
        _writer.WriteLine("</section>");
    }

    private void RegisterFootnoteDefinition(MarkdownSyntaxNode node)
    {
        var nameNode = FindChild(node, MarkdownSyntaxKind.FootnoteName);
        if (nameNode is null)
        {
            return;
        }

        var label = ExtractFootnoteLabel(nameNode);
        if (string.IsNullOrEmpty(label) || _footnoteDefinitions.ContainsKey(label))
        {
            return;
        }

        _footnoteDefinitions[label] = node;
    }

    private void WriteFootnoteReference(MarkdownSyntaxNode node)
    {
        var label = GetFirstTokenText(node);
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        var number = GetOrAddFootnoteNumber(label);
        var occurrence = NextFootnoteReferenceIndex(label);
        var referenceId = BuildFootnoteReferenceId(number, occurrence);
        var definitionId = BuildFootnoteDefinitionId(number);

        _writer.Write("<sup id=\"");
        WriteAttribute(referenceId);
        _writer.Write("\" class=\"footnote-ref\">");
        _writer.Write("<a href=\"#");
        WriteAttribute(definitionId);
        _writer.Write("\" data-footnote-ref>");
        _writer.Write(number.ToString(CultureInfo.InvariantCulture));
        _writer.Write("</a></sup>");
    }

    private int GetOrAddFootnoteNumber(string label)
    {
        if (_footnoteOrder.TryGetValue(label, out var number))
        {
            return number;
        }

        number = _footnoteOrder.Count + 1;
        _footnoteOrder[label] = number;
        _footnoteOrderList.Add(label);
        return number;
    }

    private int NextFootnoteReferenceIndex(string label)
    {
        if (_footnoteReferenceCounts.TryGetValue(label, out var count))
        {
            count++;
        }
        else
        {
            count = 1;
        }

        _footnoteReferenceCounts[label] = count;
        return count;
    }

    private static string BuildFootnoteReferenceId(int number, int occurrence) =>
        occurrence <= 1 ? $"fnref:{number}" : $"fnref:{number}:{occurrence}";

    private static string BuildFootnoteDefinitionId(int number) => $"fn:{number}";

    private static string? ExtractFootnoteLabel(MarkdownSyntaxNode node)
    {
        var token = node.ChildTokens().FirstOrDefault();
        if (token is null || string.IsNullOrEmpty(token.Text))
        {
            return null;
        }

        var span = token.Text.AsSpan().Trim();
        if (span.StartsWith("[^", StringComparison.Ordinal))
        {
            span = span[2..];
        }
        else if (span.StartsWith("[", StringComparison.Ordinal))
        {
            span = span[1..];
        }

        return span.ToString();
    }

    private static string BuildCustomContainerClass(CustomContainerParts parts)
    {
        var classes = new List<string> { "custom-container" };
        if (!string.IsNullOrEmpty(parts.Kind))
        {
            var normalizedKind = NormalizeCssIdentifier(parts.Kind);
            if (!string.IsNullOrEmpty(normalizedKind))
            {
                classes.Add($"custom-container-{normalizedKind}");
            }
        }

        if (!string.IsNullOrEmpty(parts.Identifier))
        {
            var normalizedId = NormalizeCssIdentifier(parts.Identifier);
            if (!string.IsNullOrEmpty(normalizedId))
            {
                classes.Add($"custom-container-id-{normalizedId}");
            }
        }

        return string.Join(' ', classes);
    }

    private static string NormalizeCssIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string NormalizeDataAttributeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (ch is '-' or '_')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('-');
            }
        }

        if (builder.Length == 0)
        {
            return string.Empty;
        }

        if (!char.IsLetter(builder[0]))
        {
            builder.Insert(0, 'm');
        }

        return builder.ToString().Trim('-');
    }

    private static bool TryParseCustomContainer(string text, out CustomContainerParts parts)
    {
        parts = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        using var reader = new StringReader(text);
        var openingLine = reader.ReadLine();
        if (openingLine is null)
        {
            return false;
        }

        var trimmedOpening = openingLine.TrimStart();
        var colonCount = CountLeadingColons(trimmedOpening);
        if (colonCount < 3)
        {
            return false;
        }

        var info = trimmedOpening[colonCount..].Trim();
        var closingMarker = new string(':', colonCount);

        var builder = new StringBuilder();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.TrimStart();
            if (IsClosingCustomContainerLine(trimmed, closingMarker))
            {
                break;
            }

            builder.AppendLine(line);
        }

        var content = builder.ToString();
        var (kind, identifier, metadata) = ParseCustomContainerInfo(info);
        parts = new CustomContainerParts(info, kind, identifier, metadata, content);
        return true;
    }

    private static int CountLeadingColons(string text)
    {
        var count = 0;
        while (count < text.Length && text[count] == ':')
        {
            count++;
        }

        return count;
    }

    private static bool IsClosingCustomContainerLine(string trimmed, string marker)
    {
        if (trimmed.Length < marker.Length)
        {
            return false;
        }

        if (!trimmed.StartsWith(marker, StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.Length == marker.Length)
        {
            return true;
        }

        return char.IsWhiteSpace(trimmed[marker.Length]);
    }

    private static (string? kind, string? identifier, IReadOnlyDictionary<string, string> metadata) ParseCustomContainerInfo(string info)
    {
        var tokens = TokenizeInfo(info);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? kind = null;
        string? identifier = null;
        var index = 0;

        while (index < tokens.Count && string.IsNullOrWhiteSpace(tokens[index]))
        {
            index++;
        }

        if (index < tokens.Count)
        {
            kind = tokens[index].Trim();
            index++;
        }

        if (index < tokens.Count && !tokens[index].Contains('='))
        {
            identifier = tokens[index].Trim();
            index++;
        }

        for (; index < tokens.Count; index++)
        {
            var token = tokens[index].Trim();
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            var equalsIndex = token.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex >= token.Length - 1)
            {
                continue;
            }

            var key = token[..equalsIndex].Trim();
            var value = token[(equalsIndex + 1)..].Trim();
            metadata[key] = TrimQuotes(value);
        }

        return (kind, identifier, metadata);
    }

    private static List<string> TokenizeInfo(string info)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(info))
        {
            return tokens;
        }

        var current = new StringBuilder();
        var inQuotes = false;
        char quote = '\0';

        foreach (var ch in info)
        {
            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            if (ch is '"' or '\'')
            {
                if (inQuotes && ch == quote)
                {
                    inQuotes = false;
                }
                else if (!inQuotes)
                {
                    inQuotes = true;
                    quote = ch;
                }
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static string TrimQuotes(string value)
    {
        if (value.Length >= 2)
        {
            var first = value[0];
            var last = value[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return value[1..^1];
            }
        }

        return value;
    }

    private readonly record struct CustomContainerParts(
        string RawInfo,
        string? Kind,
        string? Identifier,
        IReadOnlyDictionary<string, string> Metadata,
        string Content);

    private static (string href, string? title) ParseLinkDestination(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (string.Empty, null);
        }

        var span = raw.Trim();
        string? title = null;

        if (span.Length >= 2 && span[0] == '<' && span[^1] == '>')
        {
            span = span[1..^1];
        }

        var doubleQuoteStart = span.IndexOf('"');
        if (doubleQuoteStart >= 0)
        {
            var doubleQuoteEnd = span.LastIndexOf('"');
            if (doubleQuoteEnd > doubleQuoteStart)
            {
                title = span[(doubleQuoteStart + 1)..doubleQuoteEnd].Trim();
                span = span[..doubleQuoteStart].Trim();
                return (span, title);
            }
        }

        var singleQuoteStart = span.IndexOf('\'');
        if (singleQuoteStart >= 0)
        {
            var singleQuoteEnd = span.LastIndexOf('\'');
            if (singleQuoteEnd > singleQuoteStart)
            {
                title = span[(singleQuoteStart + 1)..singleQuoteEnd].Trim();
                span = span[..singleQuoteStart].Trim();
            }
        }

        return (span.Trim(), title);
    }

}
