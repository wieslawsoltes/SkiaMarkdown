using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace SkiaMarkdown.Rendering.Layout;

/// <summary>
/// Root layout element representing the entire document.
/// </summary>
public sealed class DocumentLayout : LayoutElement
{
    public DocumentLayout(SKRect bounds, IEnumerable<LayoutElement> children)
        : base(bounds, children)
    {
    }

    public IEnumerable<LayoutElement> EnumerateDrawables()
    {
        var stack = new Stack<LayoutElement>(Children.Reverse());
        while (stack.Count > 0)
        {
            var element = stack.Pop();
            yield return element;

            for (var i = element.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(element.Children[i]);
            }
        }
    }
}
