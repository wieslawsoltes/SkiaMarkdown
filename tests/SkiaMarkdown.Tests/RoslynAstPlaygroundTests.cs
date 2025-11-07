namespace SkiaMarkdown.Tests;

using System;
using System.Collections.Generic;
using SkiaMarkdown.Syntax;
using SkiaMarkdown.Syntax.Red;
using Xunit;

public sealed class RoslynAstPlaygroundTests
{
    private const string PlaygroundMarkdown = """
# SkiaMarkdown Preview

- [x] Render GitHub Flavored Markdown
- [x] Scroll with virtualization
- [ ] Navigate inline links like [`/Users/wieslawsoltes/GitHub/Avalonia`](file:///Users/wieslawsoltes/GitHub/Avalonia)

```csharp
var view = new MarkdownView
{
    Markdown = File.ReadAllText("README.md"),
    CanvasWidth = 960
};
```
""";

    [Fact]
    public void MarkdownSyntaxTree_Parse_PlaygroundMarkdown_DoesNotThrow()
    {
        var nodes = new List<MarkdownSyntaxNode>();
        Exception? error = null;

        try
        {
            var tree = MarkdownSyntaxTree.Parse(PlaygroundMarkdown);
            var root = tree.GetRoot();
            nodes.Add(root);
        }
        catch (Exception ex)
        {
            error = ex;
            nodes.Clear();
        }

        Assert.Null(error);
        var node = Assert.Single(nodes);
        Assert.Equal(MarkdownSyntaxKind.MarkdownDocument, node.Kind);
    }
}
