using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using SkiaMarkdown.Syntax;
using SkiaMarkdown.Syntax.Red;
using SkiaMarkdown.Syntax.Semantics;
using Xunit;

namespace SkiaMarkdown.Tests;

public class CMarkComparisonTests
{
    private const int ExampleLimit = 50;
    private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.Compiled);
    private static readonly IReadOnlyList<GfmExample> Cases = GfmSpecLoader.LoadExamples(ExampleLimit);

    public static IEnumerable<object[]> GetCases() =>
        Cases.Select(c => new object[] { c });

    [SkippableTheory]
    [MemberData(nameof(GetCases))]
    public void CMark_Output_Matches_FlattenedSemantics(GfmExample example)
    {
        var expectedPlain = NormalizeWhitespace(HtmlToPlainText(example.Html.Replace("→", "\t")));

        var tree = MarkdownSyntaxTree.Parse(example.Markdown);
        var root = tree.GetRoot();
        var flattened = NormalizeWhitespace(FlattenDocument(tree, root));

        Assert.Equal(expectedPlain, flattened);
    }

    private static string FlattenDocument(MarkdownSyntaxTree tree, MarkdownSyntaxNode root)
    {
        var builder = new StringBuilder();
        foreach (var block in root.ChildNodes())
        {
            switch (block.Kind)
            {
                case MarkdownSyntaxKind.TableBlock:
                    var table = tree.GetTableSemantic(block, out var tableDiagnostics);
                    Assert.DoesNotContain(tableDiagnostics, d => d.Severity == MarkdownDiagnosticSeverity.Error);

                    builder.AppendLine(string.Join(" | ", table.Header.Cells.Select(c => Flatten(c.Inlines))));
                    builder.AppendLine(string.Join(", ", table.Alignments));
                    foreach (var row in table.Body)
                    {
                        builder.AppendLine(string.Join(" | ", row.Cells.Select(c => Flatten(c.Inlines))));
                    }

                    break;

                default:
                    var semantics = tree.GetInlineSemantics(block, out var diagnostics);
                    Assert.DoesNotContain(diagnostics, d => d.Severity == MarkdownDiagnosticSeverity.Error);
                    if (semantics.Count > 0)
                    {
                        builder.AppendLine(Flatten(semantics));
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    private static string Flatten(IReadOnlyList<MarkdownInlineSemantic> semantics)
    {
        var builder = new StringBuilder();
        foreach (var inline in semantics)
        {
            switch (inline)
            {
                case TextInlineSemantic text:
                    builder.Append(text.Text);
                    break;
                case CodeSpanInlineSemantic code:
                    builder.Append(code.Text);
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
                case HtmlInlineSemantic html:
                    builder.Append(html.Html);
                    break;
                case EntityInlineSemantic entity:
                    builder.Append(WebUtility.HtmlDecode(entity.Value));
                    break;
                case BreakInlineSemantic br:
                    builder.Append(br.IsHard ? "\n" : " ");
                    break;
                case TaskListMarkerInlineSemantic task:
                    builder.Append(task.IsChecked ? "[x]" : "[ ]");
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
                case FootnoteReferenceInlineSemantic footnote:
                    builder.Append($"[{footnote.Label}]");
                    break;
            }
        }

        return builder.ToString();
    }

    private static string NormalizeWhitespace(string value) =>
        string.Join('\n', value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()))
            .Replace("\t", "→");

    private static string HtmlToPlainText(string html)
    {
        var withoutTags = HtmlTagRegex.Replace(html, string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }
}
