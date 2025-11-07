using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SkiaMarkdown.AvaloniaPlayground;

public sealed class AstNodeViewModel
{
    public AstNodeViewModel(string title, string? details = null)
    {
        Title = title;
        Details = details;
    }

    public string Title { get; }

    public string? Details { get; }

    public ObservableCollection<AstNodeViewModel> Children { get; } = new();

    public static AstNodeViewModel Create(string title, string? details = null, IEnumerable<AstNodeViewModel>? children = null)
    {
        var node = new AstNodeViewModel(title, details);
        if (children is not null)
        {
            foreach (var child in children)
            {
                node.Children.Add(child);
            }
        }

        return node;
    }

    public static AstNodeViewModel CreateError(string title, string message) => Create(title, message);
}
