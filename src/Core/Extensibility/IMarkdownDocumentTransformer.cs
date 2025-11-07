using SkiaMarkdown.Core.Syntax;

namespace SkiaMarkdown.Core.Extensibility;

public interface IMarkdownDocumentTransformer
{
    void Transform(MarkdownDocument document);
}
