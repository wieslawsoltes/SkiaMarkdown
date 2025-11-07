using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using SkiaMarkdown.Avalonia.Controls;
using SkiaMarkdown.Core.Pipeline;
using SkiaMarkdown.Core.Syntax;
using SkiaMarkdown.Rendering.DependencyInjection;
using AvaloniaEdit.Document;
using SkiaMarkdown.Syntax;
using SkiaMarkdown.Syntax.Red;

namespace SkiaMarkdown.AvaloniaPlayground;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly MarkdownPipeline _pipeline;
    private readonly IServiceProvider _serviceProvider;
    private readonly TextDocument _markdownDocument;
    private AstNodeViewModel? _selectedMarkdownNode;
    private AstNodeViewModel? _selectedRoslynNode;
    private bool _suppressSelectionNotifications;
    private SourceSpan? _lastSelection;

    public MainWindowViewModel()
    {
        var services = new ServiceCollection();
        services.AddSkiaMarkdownRendering();
        _serviceProvider = services.BuildServiceProvider();
        _pipeline = _serviceProvider.GetRequiredService<MarkdownPipeline>();

        _markdownDocument = new TextDocument(MarkdownView.DesignPreviewMarkdown);
        _markdownDocument.Changed += (_, _) =>
        {
            UpdateMarkdownAst();
            UpdateRoslynAst();
            OnPropertyChanged(nameof(MarkdownSource));
        };

        UpdateMarkdownAst();
        UpdateRoslynAst();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<SourceSpan>? DocumentSelectionRequested;

    public TextDocument MarkdownDocument => _markdownDocument;

    public string MarkdownSource => _markdownDocument.Text;

    public ObservableCollection<AstNodeViewModel> MarkdownAst { get; } = new();

    public ObservableCollection<AstNodeViewModel> RoslynAst { get; } = new();

    public AstNodeViewModel? SelectedMarkdownNode
    {
        get => _selectedMarkdownNode;
        set => SetSelectedNode(ref _selectedMarkdownNode, value);
    }

    public AstNodeViewModel? SelectedRoslynNode
    {
        get => _selectedRoslynNode;
        set => SetSelectedNode(ref _selectedRoslynNode, value);
    }

    public void UpdateSelectionFromText(int start, int length)
    {
        var documentLength = _markdownDocument.TextLength;
        var normalizedStart = Math.Clamp(start, 0, documentLength);
        var normalizedLength = Math.Clamp(length, 0, Math.Max(0, documentLength - normalizedStart));

        var selection = new SourceSpan(normalizedStart, normalizedLength);
        _lastSelection = selection;

        SuppressSelectionNotifications(() =>
        {
            SelectedMarkdownNode = FindBestNodeForSelection(MarkdownAst, selection);
            SelectedRoslynNode = FindBestNodeForSelection(RoslynAst, selection);
        });
    }

    private void UpdateMarkdownAst()
    {
        var nodes = new List<AstNodeViewModel>();
        MarkdownDocument? document = null;

        try
        {
            var markdown = _markdownDocument?.Text ?? string.Empty;
            document = _pipeline.Parse(markdown.AsSpan());
            foreach (var block in document.Blocks)
            {
                nodes.Add(CreateBlockNode(document, block));
            }
        }
        catch (Exception ex)
        {
            nodes.Clear();
            nodes.Add(AstNodeViewModel.CreateError("Markdown Parse Error", ex.Message));
        }
        finally
        {
            document?.Dispose();
        }

        ResetCollection(MarkdownAst, nodes);
        RestoreMarkdownSelection();
    }

    private void UpdateRoslynAst()
    {
        var nodes = new List<AstNodeViewModel>();
        try
        {
            var markdown = _markdownDocument?.Text ?? string.Empty;
            var tree = MarkdownSyntaxTree.Parse(markdown);
            var root = tree.GetRoot();
            nodes.Add(CreateRoslynNode(root));
        }
        catch (Exception ex)
        {
            nodes.Clear();
            nodes.Add(AstNodeViewModel.CreateError("Roslyn AST Error", ex.Message));
        }

        ResetCollection(RoslynAst, nodes);
        RestoreRoslynSelection();
    }

    private void OnSelectedNodeChanged(AstNodeViewModel? node)
    {
        if (_suppressSelectionNotifications || node?.Span is null)
        {
            return;
        }

        DocumentSelectionRequested?.Invoke(this, node.Span.Value);
    }

    private void SuppressSelectionNotifications(Action action)
    {
        var previous = _suppressSelectionNotifications;
        _suppressSelectionNotifications = true;
        try
        {
            action();
        }
        finally
        {
            _suppressSelectionNotifications = previous;
        }
    }

    private static AstNodeViewModel? FindBestNodeForSelection(IEnumerable<AstNodeViewModel> roots, SourceSpan selection)
    {
        AstNodeViewModel? best = null;

        foreach (var node in EnumerateNodes(roots))
        {
            if (node.Span is not SourceSpan span)
            {
                continue;
            }

            if (!ContainsSpan(span, selection))
            {
                continue;
            }

            if (best is null)
            {
                best = node;
                continue;
            }

            if (best.Span is not SourceSpan bestSpan || IsBetterCandidate(span, bestSpan))
            {
                best = node;
            }
        }

        return best;
    }

    private static bool IsBetterCandidate(SourceSpan candidate, SourceSpan current)
    {
        var candidateLength = Math.Max(candidate.Length, 1);
        var currentLength = Math.Max(current.Length, 1);

        return candidateLength < currentLength ||
               (candidateLength == currentLength && candidate.Start >= current.Start);
    }

    private static IEnumerable<AstNodeViewModel> EnumerateNodes(IEnumerable<AstNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in EnumerateNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    private static bool ContainsSpan(SourceSpan container, SourceSpan selection)
    {
        var containerStart = container.Start;
        var containerEnd = containerStart + Math.Max(container.Length, 0);
        var selectionStart = selection.Start;
        var selectionEnd = selectionStart + Math.Max(selection.Length, 0);

        if (selection.Length <= 0)
        {
            return selectionStart >= containerStart && selectionStart <= containerEnd;
        }

        return selectionStart >= containerStart && selectionEnd <= containerEnd;
    }

    private void RestoreMarkdownSelection()
    {
        SuppressSelectionNotifications(() =>
        {
            SelectedMarkdownNode = _lastSelection is SourceSpan selection
                ? FindBestNodeForSelection(MarkdownAst, selection)
                : null;
        });
    }

    private void RestoreRoslynSelection()
    {
        SuppressSelectionNotifications(() =>
        {
            SelectedRoslynNode = _lastSelection is SourceSpan selection
                ? FindBestNodeForSelection(RoslynAst, selection)
                : null;
        });
    }

    private static void EnsureNodeVisible(AstNodeViewModel? node)
    {
        while (node is not null)
        {
            node.IsExpanded = true;
            node = node.Parent;
        }
    }

    private static AstNodeViewModel CreateRoslynNode(MarkdownSyntaxNode node)
    {
        var children = new List<AstNodeViewModel>();

        foreach (var child in node.ChildNodes())
        {
            children.Add(CreateRoslynNode(child));
        }

        foreach (var token in node.ChildTokens())
        {
            children.Add(CreateTokenNode(token));
        }

        return AstNodeViewModel.Create(node.Kind.ToString(), null, children, ToSourceSpan(node.Span));
    }

    private static AstNodeViewModel CreateTokenNode(MarkdownSyntaxToken token)
    {
        var children = new List<AstNodeViewModel>();
        var fullSpan = token.Span;
        var offset = fullSpan.Start;

        AppendTriviaNodes(children, token.LeadingTrivia, ref offset, "Leading");

        var textSpan = GetTokenTextSpan(token);
        children.Add(AstNodeViewModel.Create("Text", FormatDisplayText(token.Text), span: textSpan));
        offset = textSpan.End;

        AppendTriviaNodes(children, token.TrailingTrivia, ref offset, "Trailing");

        return AstNodeViewModel.Create($"Token: {token.Kind}", null, children, ToSourceSpan(token.Span));
    }

    private static void AppendTriviaNodes(
        ICollection<AstNodeViewModel> target,
        IReadOnlyList<MarkdownSyntaxTrivia> trivia,
        ref int offset,
        string label)
    {
        if (trivia.Count == 0)
        {
            return;
        }

        foreach (var triviaItem in trivia)
        {
            var length = triviaItem.Text.Length;
            var span = new SourceSpan(offset, length);
            target.Add(AstNodeViewModel.Create(
                $"{label}Trivia ({triviaItem.Kind})",
                FormatDisplayText(triviaItem.Text),
                span: span));
            offset += length;
        }
    }

    private static string FormatDisplayText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(empty)";
        }

        return text
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static SourceSpan GetTokenTextSpan(MarkdownSyntaxToken token)
    {
        var fullSpan = token.Span;
        var leadingWidth = GetTriviaWidth(token.LeadingTrivia);
        var start = fullSpan.Start + leadingWidth;
        var length = token.Text.Length;
        return new SourceSpan(start, length);
    }

    private static int GetTriviaWidth(IReadOnlyList<MarkdownSyntaxTrivia> trivia)
    {
        if (trivia.Count == 0)
        {
            return 0;
        }

        var width = 0;
        foreach (var triviaItem in trivia)
        {
            width += triviaItem.Text.Length;
        }

        return width;
    }

    private static AstNodeViewModel CreateBlockNode(MarkdownDocument document, MarkdownBlock block)
    {
        var nodeSpan = ToSourceSpan(block.Span);
        var spanText = document.GetText(block.Span).ToString();
        return block switch
        {
            MarkdownHeading heading => AstNodeViewModel.Create(
                $"Heading (Level {heading.Level})",
                spanText,
                heading.Inlines.Select(inline => CreateInlineNode(document, inline)),
                nodeSpan),
            MarkdownParagraph paragraph => AstNodeViewModel.Create(
                "Paragraph",
                spanText,
                paragraph.Inlines.Select(inline => CreateInlineNode(document, inline)),
                nodeSpan),
            MarkdownCodeBlock code => AstNodeViewModel.Create(
                "CodeBlock",
                spanText,
                span: nodeSpan),
            MarkdownList list => AstNodeViewModel.Create(
                list.IsOrdered ? "OrderedList" : "BulletList",
                $"Items: {list.Items.Count}",
                list.Items.Select(item => CreateListItemNode(document, item)),
                nodeSpan),
            MarkdownBlockQuote quote => AstNodeViewModel.Create(
                "BlockQuote",
                spanText,
                quote.Blocks.Select(child => CreateBlockNode(document, child)),
                nodeSpan),
            MarkdownHtmlBlock html => AstNodeViewModel.Create(
                "HtmlBlock",
                spanText,
                span: nodeSpan),
            MarkdownTable table => AstNodeViewModel.Create(
                "Table",
                $"Columns: {table.Alignments.Count}",
                table.Rows.Select(row =>
                    AstNodeViewModel.Create("Row", null, row.Cells.Select(cell =>
                        AstNodeViewModel.Create(
                            "Cell",
                            document.GetText(cell.Span).ToString(),
                            span: ToSourceSpan(cell.Span))),
                        span: ToSourceSpan(row.Span))),
                nodeSpan),
            MarkdownCustomContainer custom => AstNodeViewModel.Create(
                $"CustomContainer ({custom.Info})",
                spanText,
                custom.Blocks.Select(child => CreateBlockNode(document, child)),
                nodeSpan),
            _ => AstNodeViewModel.Create(block.GetType().Name, spanText, span: nodeSpan)
        };
    }

    private static AstNodeViewModel CreateListItemNode(MarkdownDocument document, MarkdownListItem item)
    {
        var span = ToSourceSpan(item.Span);
        return AstNodeViewModel.Create(
            "ListItem",
            null,
            item.Blocks.Select(block => CreateBlockNode(document, block)),
            span);
    }

    private static AstNodeViewModel CreateInlineNode(MarkdownDocument document, MarkdownInline inline)
    {
        var spanText = document.GetText(inline.Span).ToString();
        var span = ToSourceSpan(inline.Span);
        return inline switch
        {
            MarkdownText => AstNodeViewModel.Create("Text", spanText, span: span),
            _ => AstNodeViewModel.Create(inline.Kind.ToString(), spanText, span: span)
        };
    }

    private static SourceSpan ToSourceSpan(SkiaMarkdown.Core.Syntax.TextSpan span) => new(span.Start, span.Length);

    private static SourceSpan ToSourceSpan(SkiaMarkdown.Syntax.TextSpan span) => new(span.Start, span.Length);

    private static void ResetCollection(ObservableCollection<AstNodeViewModel> collection, IEnumerable<AstNodeViewModel> nodes)
    {
        collection.Clear();
        foreach (var node in nodes)
        {
            node.AssignParentRecursive(null);
            collection.Add(node);
        }
    }

    private void SetSelectedNode(ref AstNodeViewModel? field, AstNodeViewModel? value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<AstNodeViewModel?>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        EnsureNodeVisible(value);
        OnPropertyChanged(propertyName);
        OnSelectedNodeChanged(value);
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
