using System.Net;
using System.Text;
using System.Text.Json;
using System.Linq;
using SkiaMarkdown.Syntax;
using SkiaMarkdown.Syntax.Semantics;
using SkiaMarkdown.Syntax.Red;

namespace SkiaMarkdown.Tests;

public class GitHubSpecTests
{
    private static readonly IReadOnlyList<SpecCase> Cases = LoadSpecCases();

    public static IEnumerable<object[]> GetSpecCases() =>
        Cases.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(GetSpecCases))]
    public void Spec_Cases_Parse_And_Flatten_Text(SpecCase test)
    {
        var options = MarkdownSyntaxOptions.Default;
        var tree = MarkdownSyntaxTree.Parse(test.Markdown, options);
        var root = tree.GetRoot();

        var actualBuilder = new StringBuilder();
        foreach (var block in EnumerateParagraphBlocks(root))
        {
            var semantics = tree.GetInlineSemantics(block, out var diagnostics);
            Assert.DoesNotContain(diagnostics, d => d.Severity == MarkdownDiagnosticSeverity.Error);
            actualBuilder.Append(Flatten(semantics));
        }

        var actual = actualBuilder.ToString().Trim();
        Assert.Equal(test.ExpectedText, actual);
    }

    private static IEnumerable<MarkdownSyntaxNode> EnumerateParagraphBlocks(MarkdownSyntaxNode node)
    {
        foreach (var child in node.ChildNodes())
        {
            if (child.Kind == MarkdownSyntaxKind.ParagraphBlock)
            {
                yield return child;
            }

            foreach (var nested in EnumerateParagraphBlocks(child))
            {
                yield return nested;
            }
        }
    }

    private static string Flatten(IEnumerable<MarkdownInlineSemantic> semantics)
    {
        var list = semantics as IReadOnlyList<MarkdownInlineSemantic> ?? semantics.ToList();
        var builder = new StringBuilder();
        for (var i = 0; i < list.Count; i++)
        {
            var inline = list[i];
            var isLast = i == list.Count - 1;
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
                    if (!isLast)
                    {
                        builder.Append(br.IsHard ? "\n" : " ");
                    }
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

    private static IReadOnlyList<SpecCase> LoadSpecCases()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "gfm_spec_samples.json");
        var json = File.ReadAllText(dataPath);
        return JsonSerializer.Deserialize<List<SpecCase>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<SpecCase>();
    }

    public sealed record SpecCase(int Number, string Section, string Markdown, string ExpectedText);
}
