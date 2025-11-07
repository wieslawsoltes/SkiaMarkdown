using System.Text.Json;
using SkiaMarkdown.Html;

namespace SkiaMarkdown.Html.Tests;

public class SpecDrivenHtmlTests
{
    public static IEnumerable<object[]> GetSpecCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "spec_html_cases.json");
        var json = File.ReadAllText(path);
        var cases = JsonSerializer.Deserialize<List<SpecHtmlCase>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<SpecHtmlCase>();

        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(GetSpecCases))]
    public void Spec_Cases_Render_As_Expected(SpecHtmlCase test)
    {
        var html = MarkdownHtmlGenerator.Generate(test.Markdown);
        Assert.Equal(test.ExpectedHtml, html);
    }

    public sealed record SpecHtmlCase(int Number, string Section, string Markdown, string ExpectedHtml);
}
