using SkiaMarkdown.Syntax;
using SkiaMarkdown.Syntax.Red;
using SkiaMarkdown.Syntax.Semantics;
using Xunit;

namespace SkiaMarkdown.Tests;

public class CompatibilityToggleTests
{
    [Fact]
    public void Strikethrough_Disabled_TreatsTildesAsText()
    {
        var options = MarkdownSyntaxOptions.Default with { EnableStrikethrough = false };
        var tree = MarkdownSyntaxTree.Parse("This ~~text~~ example.", options);
        var paragraph = tree.GetRoot().ChildNodes().First(n => n.Kind == MarkdownSyntaxKind.ParagraphBlock);

        var semantics = tree.GetInlineSemantics(paragraph, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == MarkdownDiagnosticSeverity.Error));

        var flattened = Flatten(semantics);
        Assert.Equal("This ~~text~~ example.", flattened);
        Assert.DoesNotContain(semantics, inline => inline is StrikethroughInlineSemantic);
    }

    [Fact]
    public void TableAlignment_Disabled_ReturnsNoneForAllColumns()
    {
        var options = MarkdownSyntaxOptions.Default with { EnableTableAlignment = false };
        const string markdown = """
| Left | Center | Right |
|:-----|:------:|------:|
| a    | b      | c     |
""";

        var tree = MarkdownSyntaxTree.Parse(markdown, options);
        var root = tree.GetRoot();
        var table = root.ChildNodes().First(n => n.Kind == MarkdownSyntaxKind.TableBlock);

        var tableSemantic = tree.GetTableSemantic(table, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == MarkdownDiagnosticSeverity.Error));

        Assert.All(tableSemantic.Alignments, alignment => Assert.Equal(MarkdownTableColumnAlignment.None, alignment));
    }

    [Fact]
    public void Footnotes_Disabled_TreatsDefinitionAsParagraph()
    {
        var options = MarkdownSyntaxOptions.Default with { EnableFootnotes = false };
        const string markdown = """
Here is a footnote reference.[^1]

[^1]: Footnote text.
""";

        var tree = MarkdownSyntaxTree.Parse(markdown, options);
        var root = tree.GetRoot();

        Assert.DoesNotContain(root.ChildNodes(), n => n.Kind == MarkdownSyntaxKind.FootnoteDefinitionBlock);

        var paragraphs = root.ChildNodes().Where(n => n.Kind == MarkdownSyntaxKind.ParagraphBlock).ToList();
        Assert.True(paragraphs.Count >= 2);

        var semantics = tree.GetInlineSemantics(paragraphs[0], out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == MarkdownDiagnosticSeverity.Error));
        Assert.Equal("Here is a footnote reference.[^1]", Flatten(semantics));
    }

    private static string Flatten(IReadOnlyList<MarkdownInlineSemantic> semantics)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var inline in semantics)
        {
            switch (inline)
            {
                case TextInlineSemantic text:
                    builder.Append(text.Text);
                    break;
                case EmphasisInlineSemantic emphasis:
                    builder.Append(Flatten(emphasis.Children));
                    break;
                case StrongInlineSemantic strong:
                    builder.Append(Flatten(strong.Children));
                    break;
                case StrikethroughInlineSemantic strike:
                    builder.Append(Flatten(strike.Children));
                    break;
                case HighlightInlineSemantic highlight:
                    builder.Append(Flatten(highlight.Children));
                    break;
                case LinkInlineSemantic link:
                    builder.Append(Flatten(link.Children));
                    break;
                case ImageInlineSemantic image:
                    builder.Append(Flatten(image.AlternativeText));
                    break;
                case AutolinkInlineSemantic autolink:
                    builder.Append(autolink.Url);
                    break;
                case EntityInlineSemantic entity:
                    builder.Append(entity.Value);
                    break;
                case BreakInlineSemantic br:
                    builder.Append(br.IsHard ? "\n" : " ");
                    break;
                case TaskListMarkerInlineSemantic task:
                    builder.Append(task.IsChecked ? "[x]" : "[ ]");
                    break;
                case FootnoteReferenceInlineSemantic footnote:
                    builder.Append($"[{footnote.Label}]");
                    break;
                case MathInlineSemantic math:
                    builder.Append(math.Expression);
                    break;
                case EmojiInlineSemantic emoji:
                    builder.Append(emoji.Shortcode);
                    break;
                case MentionInlineSemantic mention:
                    builder.Append(mention.Identifier);
                    break;
                case CodeSpanInlineSemantic code:
                    builder.Append(code.Text);
                    break;
            }
        }

        return builder.ToString();
    }
}
