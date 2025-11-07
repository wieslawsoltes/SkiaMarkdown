using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SkiaMarkdown.AvaloniaPlayground;

public sealed class AstNodeViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;

    public AstNodeViewModel(string title, string? details = null, SourceSpan? span = null)
    {
        Title = title;
        Details = details;
        Span = span;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }

    public string? Details { get; }

    public SourceSpan? Span { get; }

    public ObservableCollection<AstNodeViewModel> Children { get; } = new();

    public AstNodeViewModel? Parent { get; private set; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public static AstNodeViewModel Create(
        string title,
        string? details = null,
        IEnumerable<AstNodeViewModel>? children = null,
        SourceSpan? span = null)
    {
        var node = new AstNodeViewModel(title, details, span);
        if (children is not null)
        {
            foreach (var child in children)
            {
                node.AddChild(child);
            }
        }

        return node;
    }

    public static AstNodeViewModel CreateError(string title, string message) => Create(title, message);

    internal void AssignParentRecursive(AstNodeViewModel? parent)
    {
        Parent = parent;
        foreach (var child in Children)
        {
            child.AssignParentRecursive(this);
        }
    }

    private void AddChild(AstNodeViewModel child)
    {
        child.AssignParentRecursive(this);
        Children.Add(child);
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
