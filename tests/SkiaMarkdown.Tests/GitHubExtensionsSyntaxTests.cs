using SkiaMarkdown.Syntax;
using SkiaMarkdown.Syntax.Red;
using SkiaMarkdown.Syntax.Semantics;

namespace SkiaMarkdown.Tests;

public class GitHubExtensionsSyntaxTests
{
    [Fact]
    public void TableAlignment_IsCapturedInSyntaxAndSemantics()
    {
        const string markdown = """
| Left | Center | Right |
|:-----|:------:|-----:|
| a    | b      | c     |
""";

        var tree = MarkdownSyntaxTree.Parse(markdown);
        var root = tree.GetRoot();
        var table = root.ChildNodes().First(node => node.Kind == MarkdownSyntaxKind.TableBlock);

        var paragraphNode = table.ChildNodes().First(n => n.Kind == MarkdownSyntaxKind.TableHeader);
        Assert.Equal(MarkdownSyntaxKind.TableHeader, paragraphNode.Kind);

        var delimiterRow = table.ChildNodes().First(n => n.Kind == MarkdownSyntaxKind.TableDelimiterRow);
        var alignmentTokens = delimiterRow.ChildNodes()
            .SelectMany(n => n.ChildNodesAndTokens())
            .Where(e => e.IsToken && e.AsToken().Kind == MarkdownSyntaxKind.TableAlignmentToken)
            .Select(e => e.AsToken().Text)
            .ToArray();

        Assert.Equal(new[] { "left", "center", "right" }, alignmentTokens);

        var tableSemantic = tree.GetTableSemantic(table, out var diagnostics);
        Assert.Empty(diagnostics);
        Assert.Equal(
            new[]
            {
                MarkdownTableColumnAlignment.Left,
                MarkdownTableColumnAlignment.Center,
                MarkdownTableColumnAlignment.Right
            },
            tableSemantic.Alignments);
    }

    [Fact]
    public void FootnoteDefinition_And_Reference_AreParsed()
    {
        const string markdown = """
Here is a footnote reference.[^1]

[^1]: Footnote text.
""";

        var tree = MarkdownSyntaxTree.Parse(markdown);
        var root = tree.GetRoot();

        var footnoteBlock = root.ChildNodes().FirstOrDefault(n => n.Kind == MarkdownSyntaxKind.FootnoteDefinitionBlock);
        Assert.NotNull(footnoteBlock);

        var footnoteName = footnoteBlock!.ChildNodes().FirstOrDefault(n => n.Kind == MarkdownSyntaxKind.FootnoteName);
        Assert.NotNull(footnoteName);
        var nameToken = footnoteName.ChildNodesAndTokens().First(e => e.IsToken).AsToken();
        Assert.Equal("[^1", nameToken.Text);

        var paragraph = root.ChildNodes().First(n => n.Kind == MarkdownSyntaxKind.ParagraphBlock);
        var inlines = tree.GetInlineSemantics(paragraph, out var diagnostics);
        Assert.Empty(diagnostics);
        Assert.Contains(inlines, inline => inline is FootnoteReferenceInlineSemantic);
    }
}
