using Microsoft.Extensions.DependencyInjection;
using SkiaMarkdown.Core.Pipeline;
using SkiaMarkdown.Core.Syntax;
using SkiaMarkdown.Rendering;
using SkiaMarkdown.Rendering.DependencyInjection;

namespace SkiaMarkdown.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddSkiaMarkdownRendering_RegistersPipelineAndRenderer()
    {
        var services = new ServiceCollection();
        services.AddSkiaMarkdownRendering();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<MarkdownPipeline>());
        Assert.NotNull(provider.GetService<MarkdownRenderer>());
    }

    [Fact]
    public void MarkdownPipeline_ParsesParagraph()
    {
        var services = new ServiceCollection();
        services.AddSkiaMarkdownRendering();
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<MarkdownPipeline>();

        using var document = pipeline.Parse("Hello **world**".AsSpan());

        Assert.Single(document.Blocks);
        var paragraph = Assert.IsType<MarkdownParagraph>(document.Blocks[0]);
        Assert.Single(paragraph.Inlines);
    }

    [Fact]
    public void MarkdownPipeline_ParsesHeading()
    {
        var services = new ServiceCollection();
        services.AddSkiaMarkdownRendering();
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<MarkdownPipeline>();

        using var document = pipeline.Parse("# Title".AsSpan());

        Assert.Single(document.Blocks);
        var heading = Assert.IsType<MarkdownHeading>(document.Blocks[0]);
        Assert.Equal(1, heading.Level);
    }
}
