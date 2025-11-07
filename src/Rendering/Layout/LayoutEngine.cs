using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkiaMarkdown.Core.Syntax;
using SkiaMarkdown.Rendering.Style;
using SkiaSharp;

namespace SkiaMarkdown.Rendering.Layout;

/// <summary>
/// Builds layout primitives for Markdown documents.
/// </summary>
public sealed class LayoutEngine
{
    public DocumentLayout Build(MarkdownDocument document, LayoutOptions options)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        options ??= LayoutOptions.Default;
        var style = options.Style;

        var children = new List<LayoutElement>();
        var horizontalPadding = style.HorizontalPadding;
        var contentWidth = Math.Max(0, options.CanvasWidth - (2 * horizontalPadding));
        var currentY = style.ParagraphSpacing;

        foreach (var block in document.Blocks)
        {
            if (block is MarkdownParagraph paragraphBlock &&
                TryCreateParagraphMedia(document, paragraphBlock, style, horizontalPadding, contentWidth, children, ref currentY))
            {
                continue;
            }

            if (block is MarkdownHtmlBlock htmlBlock &&
                TryCreateHtmlMedia(document, htmlBlock, style, horizontalPadding, contentWidth, children, ref currentY))
            {
                continue;
            }

            if (block is MarkdownCustomContainer customContainer &&
                TryCreateCustomDrawable(customContainer, style, horizontalPadding, contentWidth, children, ref currentY))
            {
                continue;
            }

            switch (block)
            {
                case MarkdownHeading heading:
                {
                    var text = ExtractInlineText(document, heading.Inlines);
                    var headingStyle = style.GetHeadingStyle(heading.Level);
                    var layout = CreateTextParagraph(
                        text,
                        horizontalPadding,
                        contentWidth,
                        headingStyle,
                        ref currentY,
                        heading,
                        document.GetText(heading.Span));
                    children.Add(layout);
                    currentY += style.HeadingSpacing;
                    break;
                }

                case MarkdownParagraph paragraph:
                {
                    var text = ExtractInlineText(document, paragraph.Inlines);
                    var layout = CreateTextParagraph(
                        text,
                        horizontalPadding,
                        contentWidth,
                        style.Body,
                        ref currentY,
                        paragraph,
                        document.GetText(paragraph.Span));
                    children.Add(layout);
                    currentY += style.ParagraphSpacing;
                    break;
                }

                case MarkdownCodeBlock codeBlock:
                {
                    var codeStyle = style.CodeBlock;
                    var text = document.GetText(codeBlock.ContentSpan).ToString();
                    var blockTop = currentY;
                    var codeCurrentY = currentY + codeStyle.Padding;

                    var paragraph = CreateTextParagraph(
                        text,
                        horizontalPadding + codeStyle.Padding,
                        Math.Max(0, contentWidth - (2 * codeStyle.Padding)),
                        codeStyle.Text,
                        ref codeCurrentY,
                        codeBlock,
                        document.GetText(codeBlock.ContentSpan));

                    var blockBottom = codeCurrentY + codeStyle.Padding;
                    var backgroundBounds = new SKRect(
                        horizontalPadding,
                        blockTop,
                        horizontalPadding + contentWidth,
                        blockBottom);

                    children.Add(new RectangleLayout(
                        backgroundBounds,
                        codeStyle.BackgroundColor,
                        codeStyle.BorderColor,
                        strokeThickness: 1f,
                        cornerRadius: codeStyle.CornerRadius));
                    children.Add(paragraph);

                    currentY = blockBottom + codeStyle.Spacing;
                    break;
                }

                case MarkdownList list:
                {
                    var listElements = CreateList(
                        document,
                        list,
                        style,
                        horizontalPadding,
                        contentWidth,
                        ref currentY);
                    children.AddRange(listElements);
                    currentY += style.List.Spacing;
                    break;
                }

                case MarkdownBlockQuote quote:
                {
                    var quoteElements = CreateBlockQuote(
                        document,
                        quote,
                        style,
                        horizontalPadding,
                        contentWidth,
                        ref currentY);
                    children.AddRange(quoteElements);
                    currentY += style.BlockquoteSpacing;
                    break;
                }

                case MarkdownThematicBreak:
                {
                    var dividerStyle = style.Divider;
                    var bounds = new SKRect(
                        horizontalPadding,
                        currentY,
                        horizontalPadding + contentWidth,
                        currentY + dividerStyle.Thickness);

                    children.Add(new DividerLayout(bounds, dividerStyle.Thickness, dividerStyle.Color));
                    currentY += dividerStyle.Thickness + dividerStyle.Spacing;
                    break;
                }
            }
        }

        var documentBounds = new SKRect(0, 0, options.CanvasWidth, Math.Max(currentY, style.ParagraphSpacing));
        return new DocumentLayout(documentBounds, children);
    }

    private static ParagraphLayout CreateTextParagraph(
        string text,
        float x,
        float maxWidth,
        TextStyle textStyle,
        ref float currentY,
        MarkdownBlock? source = null,
        ReadOnlySpan<char> rawSource = default)
    {
        using var paint = CreatePaint(textStyle);

        var measuredWidth = paint.MeasureText(text);
        var width = Math.Min(measuredWidth, maxWidth);
        var metrics = paint.FontMetrics;
        var height = metrics.Descent - metrics.Ascent;
        var baseline = currentY - metrics.Ascent;

        var bounds = new SKRect(x, currentY, x + width, currentY + height);
        currentY += height;

        var glyphOffsets = CreateGlyphOffsets(text, paint);
        var link = FindLinkDestination(rawSource, text);
        var textRun = new TextRunLayout(
            text,
            bounds,
            baseline,
            textStyle.FontSize,
            textStyle.Typeface,
            textStyle.Color,
            source: null,
            linkDestination: link,
            glyphOffsets: glyphOffsets);

        return new ParagraphLayout(bounds, new[] { textRun }, source, text);
    }

    private static IEnumerable<LayoutElement> CreateList(
        MarkdownDocument document,
        MarkdownList list,
        RendererStyle style,
        float horizontalPadding,
        float contentWidth,
        ref float currentY)
    {
        var result = new List<LayoutElement>();
        var listStyle = style.List;
        var index = list.Start;

        foreach (var item in list.Items)
        {
            foreach (var block in item.Blocks)
            {
                if (block is MarkdownParagraph paragraph)
                {
                    var text = ExtractInlineText(document, paragraph.Inlines);
                    var marker = list.IsOrdered ? $"{index}. " : "• ";
                    var displayText = marker + text;

                    var paragraphLayout = CreateTextParagraph(
                        displayText,
                        horizontalPadding + listStyle.Indent,
                        Math.Max(0, contentWidth - listStyle.Indent),
                        listStyle.Text,
                        ref currentY,
                        paragraph,
                        document.GetText(paragraph.Span));

                    result.Add(paragraphLayout);
                    currentY += listStyle.ItemSpacing;
                }
            }

            if (list.IsOrdered)
            {
                index++;
            }
        }

        if (result.Count > 0)
        {
            currentY -= listStyle.ItemSpacing;
        }

        return result;
    }

    private static IEnumerable<LayoutElement> CreateBlockQuote(
        MarkdownDocument document,
        MarkdownBlockQuote quote,
        RendererStyle style,
        float horizontalPadding,
        float contentWidth,
        ref float currentY)
    {
        var elements = new List<LayoutElement>();
        var quoteStyle = style.Blockquote;
        var quoteContentY = currentY + quoteStyle.Padding;
        var quoteTextX = horizontalPadding + quoteStyle.Indent;
        var quoteTextWidth = Math.Max(0, contentWidth - quoteStyle.Indent);
        var quoteBottom = currentY;

        foreach (var block in quote.Blocks)
        {
            if (block is MarkdownParagraph paragraph)
            {
                var text = ExtractInlineText(document, paragraph.Inlines);
                var paragraphLayout = CreateTextParagraph(
                    text,
                    quoteTextX,
                    quoteTextWidth,
                    quoteStyle.Text,
                    ref quoteContentY,
                    paragraph,
                    document.GetText(paragraph.Span));

                elements.Add(paragraphLayout);
                quoteBottom = Math.Max(quoteBottom, paragraphLayout.Bounds.Bottom);
                quoteContentY += quoteStyle.Spacing;
            }
        }

        if (elements.Count > 0)
        {
            quoteContentY -= quoteStyle.Spacing;
            quoteBottom = Math.Max(quoteBottom, elements[^1].Bounds.Bottom);
        }
        else
        {
            quoteBottom = currentY;
        }

        quoteBottom += quoteStyle.Padding;

        var barBounds = new SKRect(
            horizontalPadding,
            currentY,
            horizontalPadding + quoteStyle.BarWidth,
            quoteBottom);

        elements.Insert(0, new RectangleLayout(barBounds, quoteStyle.BarColor));
        currentY = quoteBottom;

        return elements;
    }

    private static SKPaint CreatePaint(TextStyle textStyle) => new()
    {
        Typeface = textStyle.Typeface,
        TextSize = textStyle.FontSize,
        Color = textStyle.Color,
        IsAntialias = true
    };

    private static bool TryCreateParagraphMedia(
        MarkdownDocument document,
        MarkdownParagraph paragraph,
        RendererStyle style,
        float horizontalPadding,
        float contentWidth,
        IList<LayoutElement> children,
        ref float currentY)
    {
        if (paragraph.Inlines.Count != 1 || paragraph.Inlines[0] is not MarkdownText textInline)
        {
            return false;
        }

        var raw = document.GetText(textInline.Span).ToString();
        if (!TryParseMarkdownImage(raw, out var altText, out var source))
        {
            return false;
        }

        var mediaStyle = style.Media;
        var width = Math.Min(mediaStyle.MaxImageWidth, contentWidth);
        var height = mediaStyle.DefaultImageHeight;
        var bounds = new SKRect(horizontalPadding, currentY, horizontalPadding + width, currentY + height);

        if (IsVideoSource(source))
        {
            var videoLayout = new VideoPlaceholderLayout(bounds, source, altText, posterSource: null);
            children.Add(videoLayout);
        }
        else
        {
            var imageLayout = new ImageLayout(bounds, source, altText);
            children.Add(imageLayout);
        }

        currentY = bounds.Bottom + mediaStyle.Spacing;
        return true;
    }

    private static bool TryCreateHtmlMedia(
        MarkdownDocument document,
        MarkdownHtmlBlock block,
        RendererStyle style,
        float horizontalPadding,
        float contentWidth,
        IList<LayoutElement> children,
        ref float currentY)
    {
        var html = document.GetText(block.Span).ToString();
        var mediaStyle = style.Media;
        if (TryParseHtmlTag(html, "img", out var attributes))
        {
            if (!attributes.TryGetValue("src", out var source) || string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            attributes.TryGetValue("alt", out var alt);
            var width = TryParseDimension(attributes, "width", mediaStyle.MaxImageWidth);
            width = Math.Min(width, contentWidth);
            var height = TryParseDimension(attributes, "height", mediaStyle.DefaultImageHeight);
            var bounds = new SKRect(horizontalPadding, currentY, horizontalPadding + width, currentY + height);
            children.Add(new ImageLayout(bounds, source, alt));
            currentY = bounds.Bottom + mediaStyle.Spacing;
            return true;
        }

        if (TryParseHtmlTag(html, "video", out attributes))
        {
            if (!attributes.TryGetValue("src", out var source) || string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            attributes.TryGetValue("poster", out var poster);
            attributes.TryGetValue("title", out var title);
            var width = TryParseDimension(attributes, "width", mediaStyle.MaxImageWidth);
            width = Math.Min(width, contentWidth);
            var height = TryParseDimension(attributes, "height", mediaStyle.DefaultVideoHeight);
            var bounds = new SKRect(horizontalPadding, currentY, horizontalPadding + width, currentY + height);
            children.Add(new VideoPlaceholderLayout(bounds, source, title, poster));
            currentY = bounds.Bottom + mediaStyle.Spacing;
            return true;
        }

        return false;
    }

    private static bool TryCreateCustomDrawable(
        MarkdownCustomContainer container,
        RendererStyle style,
        float horizontalPadding,
        float contentWidth,
        IList<LayoutElement> children,
        ref float currentY)
    {
        if (string.IsNullOrWhiteSpace(container.Info))
        {
            return false;
        }

        var parts = container.Info.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].Equals("drawable", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var identifier = parts.Length > 1 ? parts[1] : "custom";
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 2; i < parts.Length; i++)
        {
            var token = parts[i];
            var splitIndex = token.IndexOf('=');
            if (splitIndex <= 0)
            {
                continue;
            }

            var key = token[..splitIndex];
            var value = token[(splitIndex + 1)..].Trim('"');
            metadata[key] = value;
        }

        var mediaStyle = style.Media;
        var width = metadata.TryGetValue("width", out var widthValue) && TryParseFloat(widthValue, out var explicitWidth)
            ? Math.Min(explicitWidth, contentWidth)
            : Math.Min(mediaStyle.MaxImageWidth, contentWidth);

        var height = metadata.TryGetValue("height", out var heightValue) && TryParseFloat(heightValue, out var explicitHeight)
            ? explicitHeight
            : mediaStyle.DefaultImageHeight;

        var bounds = new SKRect(horizontalPadding, currentY, horizontalPadding + width, currentY + height);
        children.Add(new CustomDrawableLayout(bounds, identifier, metadata));
        currentY = bounds.Bottom + mediaStyle.Spacing;
        return true;
    }

    private static bool TryParseMarkdownImage(string text, out string? alternativeText, out string source)
    {
        alternativeText = null;
        source = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (!trimmed.StartsWith("![", StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = trimmed.IndexOf("](", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return false;
        }

        var closingParen = trimmed.LastIndexOf(')');
        if (closingParen <= separatorIndex + 1)
        {
            return false;
        }

        alternativeText = trimmed.Substring(2, separatorIndex - 2);
        var destination = trimmed.Substring(separatorIndex + 2, closingParen - (separatorIndex + 2));
        var whitespaceIndex = destination.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        if (whitespaceIndex >= 0)
        {
            destination = destination[..whitespaceIndex];
        }

        source = destination.Trim();
        return source.Length > 0;
    }

    private static bool IsVideoSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var lower = source.AsSpan().Trim().ToString().ToLowerInvariant();
        return lower.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
               || lower.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)
               || lower.EndsWith(".mov", StringComparison.OrdinalIgnoreCase)
               || lower.EndsWith(".m4v", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseHtmlTag(string html, string tagName, out Dictionary<string, string> attributes)
    {
        attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var search = "<" + tagName;
        var index = html.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var closing = html.IndexOf('>', index + search.Length);
        if (closing < 0)
        {
            return false;
        }

        var attributeSegment = html.Substring(index + search.Length, closing - (index + search.Length));
        attributes = ParseAttributes(attributeSegment);
        return attributes.Count > 0;
    }

    private static Dictionary<string, string> ParseAttributes(string segment)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var span = segment.AsSpan();
        var index = 0;

        while (index < span.Length)
        {
            while (index < span.Length && char.IsWhiteSpace(span[index]))
            {
                index++;
            }

            if (index >= span.Length)
            {
                break;
            }

            var nameStart = index;
            while (index < span.Length && !char.IsWhiteSpace(span[index]) && span[index] != '=' && span[index] != '/' && span[index] != '>')
            {
                index++;
            }

            if (index <= nameStart)
            {
                index++;
                continue;
            }

            var name = span[nameStart..index].ToString();

            while (index < span.Length && char.IsWhiteSpace(span[index]))
            {
                index++;
            }

            if (index >= span.Length || span[index] != '=')
            {
                result[name] = string.Empty;
                continue;
            }

            index++; // skip '='
            while (index < span.Length && char.IsWhiteSpace(span[index]))
            {
                index++;
            }

            if (index >= span.Length)
            {
                break;
            }

            string value;
            var quote = span[index];
            if (quote == '"' || quote == '\'')
            {
                index++;
                var valueStart = index;
                while (index < span.Length && span[index] != quote)
                {
                    index++;
                }

                value = span[valueStart..Math.Min(index, span.Length)].ToString();
                if (index < span.Length)
                {
                    index++;
                }
            }
            else
            {
                var valueStart = index;
                while (index < span.Length && !char.IsWhiteSpace(span[index]) && span[index] != '/' && span[index] != '>')
                {
                    index++;
                }

                value = span[valueStart..index].ToString();
            }

            result[name] = value;
        }

        return result;
    }

    private static float TryParseDimension(IReadOnlyDictionary<string, string> attributes, string key, float fallback)
    {
        if (attributes.TryGetValue(key, out var value) && TryParseFloat(value, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static bool TryParseFloat(string value, out float result) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static float[] CreateGlyphOffsets(string text, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<float>();
        }

        var offsets = new float[text.Length + 1];
        offsets[0] = 0f;
        var span = text.AsSpan();
        for (var i = 0; i < text.Length; i++)
        {
            offsets[i + 1] = paint.MeasureText(span[..(i + 1)]);
        }

        return offsets;
    }

    private static readonly Regex MarkdownLinkRegex = new(@"\[(?<text>[^\]]+)\]\((?<url>[^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(@"(?<url>(https?|ftp)://[^\s)]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string? FindLinkDestination(ReadOnlySpan<char> rawSource, string displayText)
    {
        if (rawSource.Length > 0)
        {
            var raw = rawSource.ToString();

            var mdMatch = MarkdownLinkRegex.Match(raw);
            if (mdMatch.Success)
            {
                var url = mdMatch.Groups["url"].Value.Trim();
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    return uri.ToString();
                }
            }

            var rawUrlMatch = UrlRegex.Match(raw);
            if (rawUrlMatch.Success)
            {
                var candidate = rawUrlMatch.Groups["url"].Value.TrimEnd('.', ',', ';');
                if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
                {
                    return uri.ToString();
                }
            }
        }

        if (!string.IsNullOrEmpty(displayText))
        {
            var displayMatch = UrlRegex.Match(displayText);
            if (displayMatch.Success)
            {
                var candidate = displayMatch.Groups["url"].Value.TrimEnd('.', ',', ';');
                if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
                {
                    return uri.ToString();
                }
            }
        }

        return null;
    }

    private static string ExtractInlineText(MarkdownDocument document, IEnumerable<MarkdownInline> inlines)
    {
        var builder = new StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case MarkdownText text:
                    builder.Append(document.GetText(text.Span));
                    break;
            }
        }

        return builder.ToString();
    }
}
