namespace SkiaMarkdown.Core.Extensibility;

public interface IMarkdownExtension
{
    void Configure(MarkdownPipelineBuilder builder);
}
