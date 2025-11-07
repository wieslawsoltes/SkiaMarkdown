using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SkiaMarkdown.Tests;

internal static class GfmSpecLoader
{
    private const string ExampleFence = "````````````````````````````````";
    private static readonly Regex HeadingRegex = new(@"^#{2,}\s+(.*)$", RegexOptions.Compiled);

    public static IReadOnlyList<GfmExample> LoadExamples(int? maxExamples = null)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "cmark-gfm-spec.txt");
        if (!File.Exists(path))
        {
            return Array.Empty<GfmExample>();
        }

        var lines = File.ReadAllLines(path);
        var examples = new List<GfmExample>();
        var currentSection = string.Empty;
        var index = 0;
        var exampleNumber = 0;

        while (index < lines.Length)
        {
            var line = lines[index];

            var headingMatch = HeadingRegex.Match(line);
            if (headingMatch.Success)
            {
                currentSection = headingMatch.Groups[1].Value.Trim();
                index++;
                continue;
            }

            if (line.StartsWith(ExampleFence, StringComparison.Ordinal) &&
                line.Contains("example", StringComparison.Ordinal))
            {
                exampleNumber++;
                index++;

                var markdownLines = new List<string>();
                while (index < lines.Length && lines[index] != ".")
                {
                    markdownLines.Add(NormalizeSpecLine(lines[index]));
                    index++;
                }

                if (index >= lines.Length)
                {
                    break;
                }

                index++; // skip '.' separator

                var htmlLines = new List<string>();
                while (index < lines.Length && lines[index] != ExampleFence)
                {
                    htmlLines.Add(NormalizeSpecLine(lines[index], convertArrows: false));
                    index++;
                }

                // skip closing fence
                if (index < lines.Length)
                {
                    index++;
                }

                var example = new GfmExample(
                    exampleNumber,
                    currentSection,
                    string.Join('\n', markdownLines),
                    string.Join('\n', htmlLines));

                examples.Add(example);

                if (maxExamples.HasValue && examples.Count >= maxExamples.Value)
                {
                    break;
                }

                continue;
            }

            index++;
        }

        return examples;
    }

    private static string NormalizeSpecLine(string line, bool convertArrows = true)
    {
        if (convertArrows)
        {
            return line.Replace("→", "\t");
        }

        return line;
    }
}

internal sealed record GfmExample(int Number, string Section, string Markdown, string Html);
