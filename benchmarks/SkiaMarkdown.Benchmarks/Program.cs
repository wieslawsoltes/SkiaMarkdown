using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using SkiaMarkdown.Core.Pipeline;
using SkiaMarkdown.Rendering.DependencyInjection;

BenchmarkRunner.Run<MarkdownPipelineBenchmarks>();

public sealed class MarkdownPipelineBenchmarks : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly MarkdownPipeline _pipeline;
    private readonly string _document;

    public MarkdownPipelineBenchmarks()
    {
        var services = new ServiceCollection();
        services.AddSkiaMarkdownRendering();
        _provider = services.BuildServiceProvider();
        _pipeline = _provider.GetRequiredService<MarkdownPipeline>();
        _document = SampleDocument();
    }

    [Benchmark(Description = "Parse medium GitHub flavored markdown")]
    public int ParseMediumDocument()
    {
        using var document = _pipeline.Parse(_document.AsSpan());
        return document.Blocks.Count;
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    private static string SampleDocument()
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("# SkiaMarkdown Benchmark");
        builder.AppendLine();
        for (var i = 0; i < 100; i++)
        {
            builder.AppendLine($"- [ ] Item {i}");
            builder.AppendLine("  ```csharp");
            builder.AppendLine("  Console.WriteLine(\"Hello\");");
            builder.AppendLine("  ```");
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
