using SkiaMarkdown.Html;

namespace SkiaMarkdown.Html.Tests;

public class MarkdownHtmlGeneratorTests
{
    [Fact]
    public void Heading_And_Paragraph_Are_Rendered()
    {
        var markdown = "# Title\n\nHello world";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<h1>Title</h1>\n<p>Hello world</p>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Inline_Emphasis_Is_Written()
    {
        var markdown = "Hello *em* **strong**";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<p>Hello <em>em</em> <strong>strong</strong></p>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Lists_Are_Emitted()
    {
        var markdown = "- one\n- two";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<ul>\n<li>one\n</li>\n<li>two\n</li>\n</ul>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Fenced_Code_Block_With_Info_String()
    {
        var markdown = "```csharp\nConsole.WriteLine();\n```";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<pre><code class=\"language-csharp\">Console.WriteLine();</code></pre>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Setext_Headings_Render()
    {
        var markdown = "Heading\n====";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<h1>Heading</h1>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Indented_Code_Block_Renders()
    {
        var markdown = "    code\n    line";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<pre><code>code\nline</code></pre>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Tables_Are_Rendered()
    {
        var markdown = "| h1 | h2 |\n| :-- | --: |\n| c1 | c2 |";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected =
            "<table>\n" +
            "<thead>\n" +
            "<tr><th style=\"text-align:left\">h1</th><th style=\"text-align:right\">h2</th></tr>\n" +
            "</thead>\n" +
            "<tbody>\n" +
            "<tr><td style=\"text-align:left\">c1</td><td style=\"text-align:right\">c2</td></tr>\n" +
            "</tbody>\n" +
            "</table>\n";

        Assert.Equal(expected, html);
    }

    [Fact]
    public void Links_And_Images_Render()
    {
        var markdown = "An [example](https://example.com \"title\") and ![alt](img.png)";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<p>An <a href=\"https://example.com\" title=\"title\">example</a> and <img src=\"img.png\" alt=\"alt\" /></p>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Autolinks_Render()
    {
        var markdown = "Visit <https://example.com>.";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<p>Visit <a href=\"https://example.com\">https://example.com</a>.</p>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Task_List_Items_Render_Checkboxes()
    {
        var markdown = "- [x] done\n- [ ] todo";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<ul>\n<li><input type=\"checkbox\" disabled checked /> done\n</li>\n<li><input type=\"checkbox\" disabled /> todo\n</li>\n</ul>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Entities_Are_Decoded()
    {
        var markdown = "&copy; &amp;";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<p>© &amp;</p>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Raw_Html_Is_Escaped_When_Disabled()
    {
        var markdown = "<b>raw</b>";
        var html = MarkdownHtmlGenerator.Generate(markdown, MarkdownHtmlOptions.Default with { AllowRawHtml = false });

        const string expected = "&lt;b&gt;raw&lt;/b&gt;\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Mentions_Render()
    {
        var markdown = "@user says hi";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<p><span class=\"mention\" data-mention=\"user\">@user</span> says hi</p>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Emoji_Render_WithFallback()
    {
        var markdown = ":smile:";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<p><span class=\"emoji\" data-emoji=\"smile\">:smile:</span></p>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Emoji_Resolver_Is_Used_WhenProvided()
    {
        var markdown = ":smile:";
        var options = MarkdownHtmlOptions.Default with { EmojiResolver = name => name == "smile" ? "😄" : null };
        var html = MarkdownHtmlGenerator.Generate(markdown, options);

        const string expected = "<p><span class=\"emoji\" data-emoji=\"smile\">😄</span></p>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Highlight_Rendered_As_Mark()
    {
        const string markdown = "==focus== text";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<p><mark>focus</mark> text</p>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Inline_Math_Renders_As_Span()
    {
        const string markdown = "Math $E=mc^2$ inline.";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "<p>Math <span class=\"math math-inline\" data-math=\"E=mc^2\">E=mc^2</span> inline.</p>\n";
        Assert.Equal(expected, html);
    }

    [Fact]
    public void Footnotes_Render_Reference_And_List()
    {
        const string markdown = """
Here is a footnote reference.[^1]

[^1]: Footnote text.
""";

        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected =
            "<p>Here is a footnote reference.<sup id=\"fnref:1\" class=\"footnote-ref\"><a href=\"#fn:1\" data-footnote-ref>1</a></sup></p>\n" +
            "<section class=\"footnotes\" data-footnotes>\n" +
            "<hr />\n" +
            "<ol>\n" +
            "<li id=\"fn:1\"><p>Footnote text.</p>\n" +
            " <a href=\"#fnref:1\" class=\"footnote-backref\" aria-label=\"Back to content\">↩</a></li>\n" +
            "</ol>\n" +
            "</section>\n";

        Assert.Equal(expected, html);
    }

    [Fact]
    public void Custom_Container_Renders_Metadata_And_Content()
    {
        const string markdown = """
::: drawable sparkline width=120 height=40 label="Samples"
*Inner* content.
:::
""";

        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected =
            "<div class=\"custom-container custom-container-drawable custom-container-id-sparkline\" " +
            "data-container-kind=\"drawable\" data-container-id=\"sparkline\" " +
            "data-container-info=\"drawable sparkline width=120 height=40 label=&quot;Samples&quot;\" " +
            "data-width=\"120\" data-height=\"40\" data-label=\"Samples\">\n" +
            "<p><em>Inner</em> content.</p>\n" +
            "</div>\n";

        Assert.Equal(expected, html);
    }

    [Fact]
    public void Disallowed_Raw_Html_Is_Filtered()
    {
        const string markdown = "<script>alert(1)</script> and <style>.x{}</style>";
        var html = MarkdownHtmlGenerator.Generate(markdown);

        const string expected = "&lt;script>alert(1)&lt;/script> and &lt;style>.x{}&lt;/style>\n";
        Assert.Equal(expected, html);
    }
}
