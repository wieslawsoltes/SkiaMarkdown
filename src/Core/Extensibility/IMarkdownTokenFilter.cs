using SkiaMarkdown.Core.Tokenizer;

namespace SkiaMarkdown.Core.Extensibility;

public interface IMarkdownTokenFilter
{
    MarkdownToken Filter(ReadOnlySpan<char> source, MarkdownToken token);
}
