using System;
using System.IO;
using System.Text;
using SkiaMarkdown.Syntax;
using SkiaMarkdown.Syntax.Red;

namespace SkiaMarkdown.Html;

/// <summary>
/// Converts Markdown documents (or syntax trees) into HTML.
/// </summary>
public static class MarkdownHtmlGenerator
{
    /// <summary>
    /// Generates HTML from raw Markdown text.
    /// </summary>
    public static string Generate(string markdown, MarkdownHtmlOptions? options = null)
    {
        if (markdown is null)
        {
            throw new ArgumentNullException(nameof(markdown));
        }

        var tree = MarkdownSyntaxTree.Parse(markdown, SyntaxOptions);
        return Generate(tree, options);
    }

    /// <summary>
    /// Generates HTML from a pre-parsed syntax tree.
    /// </summary>
    public static string Generate(MarkdownSyntaxTree tree, MarkdownHtmlOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return Generate(tree.GetRoot(), options);
    }

    /// <summary>
    /// Generates HTML starting at the specified syntax node.
    /// </summary>
    public static string Generate(MarkdownSyntaxNode node, MarkdownHtmlOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        using var writer = new StringWriter(new StringBuilder(capacity: 1024));
        writer.NewLine = "\n";
        var htmlWriter = new MarkdownHtmlWriter(writer, options ?? MarkdownHtmlOptions.Default);
        htmlWriter.Visit(node);
        htmlWriter.Complete();
        writer.Flush();
        return writer.ToString();
    }

    internal static MarkdownSyntaxOptions SyntaxOptions { get; } = new();
}
