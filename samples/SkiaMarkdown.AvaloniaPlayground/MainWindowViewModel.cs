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

    public TextDocument MarkdownDocument => _markdownDocument;

    public string MarkdownSource => _markdownDocument.Text;

    public ObservableCollection<AstNodeViewModel> MarkdownAst { get; } = new();

    public ObservableCollection<AstNodeViewModel> RoslynAst { get; } = new();

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
            var tokenChildren = new List<AstNodeViewModel>
            {
                AstNodeViewModel.Create("Text", token.Text)
            };

            if (token.LeadingTrivia.Count > 0)
            {
                tokenChildren.Add(AstNodeViewModel.Create(
                    "LeadingTrivia",
                    string.Join(Environment.NewLine, token.LeadingTrivia.Select(t => $"{t.Kind}: {t.Text}"))));
            }

            if (token.TrailingTrivia.Count > 0)
            {
                tokenChildren.Add(AstNodeViewModel.Create(
                    "TrailingTrivia",
                    string.Join(Environment.NewLine, token.TrailingTrivia.Select(t => $"{t.Kind}: {t.Text}"))));
            }

            children.Add(AstNodeViewModel.Create($"Token: {token.Kind}", null, tokenChildren));
        }

        return AstNodeViewModel.Create(node.Kind.ToString(), null, children);
    }

    private static AstNodeViewModel CreateBlockNode(MarkdownDocument document, MarkdownBlock block)
    {
        var spanText = document.GetText(block.Span).ToString();
        return block switch
        {
            MarkdownHeading heading => AstNodeViewModel.Create(
                $"Heading (Level {heading.Level})",
                spanText,
                heading.Inlines.Select(inline => CreateInlineNode(document, inline))),
            MarkdownParagraph paragraph => AstNodeViewModel.Create(
                "Paragraph",
                spanText,
                paragraph.Inlines.Select(inline => CreateInlineNode(document, inline))),
            MarkdownCodeBlock code => AstNodeViewModel.Create(
                "CodeBlock",
                spanText),
            MarkdownList list => AstNodeViewModel.Create(
                list.IsOrdered ? "OrderedList" : "BulletList",
                $"Items: {list.Items.Count}",
                list.Items.Select(item => CreateListItemNode(document, item))),
            MarkdownBlockQuote quote => AstNodeViewModel.Create(
                "BlockQuote",
                spanText,
                quote.Blocks.Select(child => CreateBlockNode(document, child))),
            MarkdownHtmlBlock html => AstNodeViewModel.Create(
                "HtmlBlock",
                spanText),
            MarkdownTable table => AstNodeViewModel.Create(
                "Table",
                $"Columns: {table.Alignments.Count}",
                table.Rows.Select(row =>
                    AstNodeViewModel.Create("Row", null, row.Cells.Select(cell =>
                        AstNodeViewModel.Create("Cell", document.GetText(cell.Span).ToString()))))),
            MarkdownCustomContainer custom => AstNodeViewModel.Create(
                $"CustomContainer ({custom.Info})",
                spanText,
                custom.Blocks.Select(child => CreateBlockNode(document, child))),
            _ => AstNodeViewModel.Create(block.GetType().Name, spanText)
        };
    }

    private static AstNodeViewModel CreateListItemNode(MarkdownDocument document, MarkdownListItem item)
    {
        return AstNodeViewModel.Create(
            "ListItem",
            null,
            item.Blocks.Select(block => CreateBlockNode(document, block)));
    }

    private static AstNodeViewModel CreateInlineNode(MarkdownDocument document, MarkdownInline inline)
    {
        var spanText = document.GetText(inline.Span).ToString();
        return inline switch
        {
            MarkdownText => AstNodeViewModel.Create("Text", spanText),
            _ => AstNodeViewModel.Create(inline.Kind.ToString(), spanText)
        };
    }

    private static void ResetCollection(ObservableCollection<AstNodeViewModel> collection, IEnumerable<AstNodeViewModel> nodes)
    {
        collection.Clear();
        foreach (var node in nodes)
        {
            collection.Add(node);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
