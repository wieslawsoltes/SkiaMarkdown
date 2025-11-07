using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit;

namespace SkiaMarkdown.AvaloniaPlayground;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly TextEditor? _markdownEditor;
    private readonly TreeView? _markdownTree;
    private readonly TreeView? _roslynTree;
    private bool _synchronizingTreeSelection;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = DataContext as MainWindowViewModel ?? new MainWindowViewModel();
        DataContext = _viewModel;

        _markdownEditor = this.FindControl<TextEditor>("MarkdownEditor");
        _markdownTree = this.FindControl<TreeView>("MarkdownTree");
        _roslynTree = this.FindControl<TreeView>("RoslynTree");

        if (_markdownEditor is not null)
        {
            _markdownEditor.TextArea.SelectionChanged += OnMarkdownSelectionChanged;
            _markdownEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        }

        if (_markdownTree is not null)
        {
            _markdownTree.SelectionChanged += OnMarkdownTreeSelectionChanged;
        }

        if (_roslynTree is not null)
        {
            _roslynTree.SelectionChanged += OnRoslynTreeSelectionChanged;
        }

        _viewModel.DocumentSelectionRequested += OnDocumentSelectionRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_markdownEditor is not null)
        {
            _markdownEditor.TextArea.SelectionChanged -= OnMarkdownSelectionChanged;
            _markdownEditor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        }

        if (_markdownTree is not null)
        {
            _markdownTree.SelectionChanged -= OnMarkdownTreeSelectionChanged;
        }

        if (_roslynTree is not null)
        {
            _roslynTree.SelectionChanged -= OnRoslynTreeSelectionChanged;
        }

        _viewModel.DocumentSelectionRequested -= OnDocumentSelectionRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        base.OnClosed(e);
    }

    private void OnMarkdownSelectionChanged(object? sender, EventArgs e)
    {
        if (_markdownEditor is null)
        {
            return;
        }

        _viewModel.UpdateSelectionFromText(
            _markdownEditor.SelectionStart,
            _markdownEditor.SelectionLength);
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_markdownEditor is null || _markdownEditor.SelectionLength > 0)
        {
            return;
        }

        _viewModel.UpdateSelectionFromText(_markdownEditor.CaretOffset, 0);
    }

    private void OnMarkdownTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_synchronizingTreeSelection)
        {
            return;
        }

        _viewModel.SelectedMarkdownNode = _markdownTree?.SelectedItem as AstNodeViewModel;
    }

    private void OnRoslynTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_synchronizingTreeSelection)
        {
            return;
        }

        _viewModel.SelectedRoslynNode = _roslynTree?.SelectedItem as AstNodeViewModel;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedMarkdownNode))
        {
            SyncTreeSelection(_markdownTree, _viewModel.SelectedMarkdownNode);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.SelectedRoslynNode))
        {
            SyncTreeSelection(_roslynTree, _viewModel.SelectedRoslynNode);
        }
    }

    private void SyncTreeSelection(TreeView? tree, AstNodeViewModel? node)
    {
        if (tree is null || Equals(tree.SelectedItem, node))
        {
            return;
        }

        _synchronizingTreeSelection = true;
        try
        {
            tree.SelectedItem = node;
        }
        finally
        {
            _synchronizingTreeSelection = false;
        }
    }

    private void OnDocumentSelectionRequested(object? sender, SourceSpan span)
    {
        if (_markdownEditor is null)
        {
            return;
        }

        var textLength = _markdownEditor.Document?.TextLength ?? 0;
        var start = Math.Clamp(span.Start, 0, textLength);
        var maxLength = Math.Max(0, textLength - start);
        var length = Math.Clamp(span.Length, 0, maxLength);

        _markdownEditor.Select(start, length);
        _markdownEditor.TextArea.Caret.Offset = start + length;
        _markdownEditor.TextArea.Caret.BringCaretToView();
    }
}
