using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using SkiaMarkdown.Core.Pipeline;
using SkiaMarkdown.Core.Syntax;
using SkiaMarkdown.Rendering;
using SkiaMarkdown.Rendering.DependencyInjection;
using SkiaMarkdown.Rendering.Style;

namespace SkiaMarkdown.Avalonia.Controls;

/// <summary>
/// Scrollable Avalonia control hosting the SkiaMarkdown rendering pipeline.
/// </summary>
public class MarkdownView : ScrollViewer
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    public static readonly StyledProperty<LayoutOptions> LayoutOptionsProperty =
        AvaloniaProperty.Register<MarkdownView, LayoutOptions>(nameof(LayoutOptions), LayoutOptions.Default);

    public static readonly StyledProperty<RendererStyle> RendererStyleProperty =
        AvaloniaProperty.Register<MarkdownView, RendererStyle>(nameof(RendererStyle), RendererStyle.GitHubLight);

    public static readonly StyledProperty<double> CanvasWidthProperty =
        AvaloniaProperty.Register<MarkdownView, double>(nameof(CanvasWidth), 960d);

    private const string DesignPreviewMarkdownContent = """
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

    public static string DesignPreviewMarkdown => DesignPreviewMarkdownContent;

    private readonly MarkdownPipeline _pipeline;
    private readonly MarkdownRenderer _renderer;
    private readonly MarkdownScrollPresenter _presenter;
    private readonly IServiceProvider? _serviceProvider;
    private readonly MenuFlyout _contextFlyout;
    private readonly MenuItem _copyMenuItem;
    private readonly MenuItem _copyMarkdownMenuItem;
    private readonly MenuItem _selectAllMenuItem;
    private readonly MenuItem _openLinkMenuItem;
    private readonly MenuItem _copyLinkMenuItem;
    private MarkdownDocument? _document;
    private CancellationTokenSource? _parseCancellation;
    private string _currentMarkdown = string.Empty;
    private bool _updatingLayoutOptions;
    private Point? _lastContextPoint;
    private string? _pendingContextLink;

    public MarkdownView()
        : this(CreateDefaultProvider())
    {
    }

    public MarkdownView(IServiceProvider serviceProvider)
        : this(
            serviceProvider.GetRequiredService<MarkdownPipeline>(),
            serviceProvider.GetRequiredService<MarkdownRenderer>())
    {
        _serviceProvider = serviceProvider;
    }

    public MarkdownView(MarkdownPipeline pipeline, MarkdownRenderer renderer)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));

        _presenter = new MarkdownScrollPresenter(_renderer);
        Content = _presenter;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        ApplyLayoutOptions(LayoutOptions);
        Focusable = true;
        IsTabStop = true;

        _copyMenuItem = new MenuItem { Header = "Copy" };
        _copyMenuItem.Click += OnCopyMenuItemClick;
        _copyMarkdownMenuItem = new MenuItem { Header = "Copy Markdown" };
        _copyMarkdownMenuItem.Click += OnCopyMarkdownMenuItemClick;
        _selectAllMenuItem = new MenuItem { Header = "Select All" };
        _selectAllMenuItem.Click += OnSelectAllMenuItemClick;
        _openLinkMenuItem = new MenuItem { Header = "Open Link" };
        _openLinkMenuItem.Click += OnOpenLinkMenuItemClick;
        _copyLinkMenuItem = new MenuItem { Header = "Copy Link" };
        _copyLinkMenuItem.Click += OnCopyLinkMenuItemClick;

        _contextFlyout = new MenuFlyout
        {
            Items =
            {
                _copyMenuItem,
                _copyMarkdownMenuItem,
                _selectAllMenuItem,
                new Separator(),
                _openLinkMenuItem,
                _copyLinkMenuItem
            }
        };

        ContextRequested += OnContextRequested;
    }

    public event EventHandler<MarkdownLinkEventArgs>? LinkClicked;

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public LayoutOptions LayoutOptions
    {
        get => GetValue(LayoutOptionsProperty);
        set => SetValue(LayoutOptionsProperty, value);
    }

    public RendererStyle RendererStyle
    {
        get => GetValue(RendererStyleProperty);
        set => SetValue(RendererStyleProperty, value);
    }

    public double CanvasWidth
    {
        get => GetValue(CanvasWidthProperty);
        set => SetValue(CanvasWidthProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MarkdownProperty)
        {
            UpdateDocument(change.GetNewValue<string?>());
            return;
        }

        if (_updatingLayoutOptions)
        {
            return;
        }

        if (change.Property == LayoutOptionsProperty)
        {
            var options = change.GetNewValue<LayoutOptions>() ?? LayoutOptions.Default;
            ApplyLayoutOptions(options);
        }
        else if (change.Property == RendererStyleProperty)
        {
            var style = change.GetNewValue<RendererStyle>() ?? RendererStyle.GitHubLight;
            var current = LayoutOptions;
            if (!ReferenceEquals(current.Style, style) && current.Style != style)
            {
                UpdateLayoutOptions(current with { Style = style });
            }
        }
        else if (change.Property == CanvasWidthProperty)
        {
            var width = change.GetNewValue<double>();
            var current = LayoutOptions;
            var normalized = (float)Math.Max(1d, double.IsNaN(width) ? current.CanvasWidth : width);
            if (Math.Abs(current.CanvasWidth - normalized) > 0.01f)
            {
                UpdateLayoutOptions(current with { CanvasWidth = normalized });
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _parseCancellation?.Cancel();
        _parseCancellation?.Dispose();
        _parseCancellation = null;

        _document?.Dispose();
        _document = null;
        _presenter.Document = null;
        _presenter.LinkClicked -= OnPresenterLinkClicked;
        ContextRequested -= OnContextRequested;

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _presenter.LinkClicked += OnPresenterLinkClicked;
        if (Design.IsDesignMode && string.IsNullOrEmpty(Markdown))
        {
            SetCurrentValue(MarkdownProperty, DesignPreviewMarkdownContent);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled)
        {
            return;
        }

        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            switch (e.Key)
            {
                case Key.C:
                    _ = CopySelectionAsync();
                    e.Handled = true;
                    break;
                case Key.A:
                    _presenter.SelectAll();
                    e.Handled = true;
                    break;
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MarkdownViewAutomationPeer(this);

    private static LayoutOptions NormalizeLayoutOptions(LayoutOptions options)
    {
        var style = options.Style ?? RendererStyle.GitHubLight;
        var width = options.CanvasWidth <= 0 ? LayoutOptions.Default.CanvasWidth : options.CanvasWidth;
        return options with
        {
            Style = style,
            CanvasWidth = width
        };
    }

    private void ApplyLayoutOptions(LayoutOptions options)
    {
        var normalized = NormalizeLayoutOptions(options);

        _updatingLayoutOptions = true;
        try
        {
            if (LayoutOptions != normalized)
            {
                SetCurrentValue(LayoutOptionsProperty, normalized);
            }

            SetCurrentValue(RendererStyleProperty, normalized.Style);
            SetCurrentValue(CanvasWidthProperty, (double)normalized.CanvasWidth);
        }
        finally
        {
            _updatingLayoutOptions = false;
        }

        _presenter.LayoutOptions = normalized;
    }

    private void UpdateLayoutOptions(LayoutOptions options)
    {
        var normalized = NormalizeLayoutOptions(options);

        _updatingLayoutOptions = true;
        try
        {
            if (LayoutOptions != normalized)
            {
                SetCurrentValue(LayoutOptionsProperty, normalized);
            }

            SetCurrentValue(RendererStyleProperty, normalized.Style);
            SetCurrentValue(CanvasWidthProperty, (double)normalized.CanvasWidth);
        }
        finally
        {
            _updatingLayoutOptions = false;
        }

        _presenter.LayoutOptions = normalized;
    }

    private void UpdateDocument(string? markdown)
    {
        var newText = markdown ?? string.Empty;
        var previousText = _currentMarkdown;
        var change = MarkdownTextChange.FromBuffers(previousText.AsSpan(), newText.AsSpan());

        if (!change.HasChanges && _document is not null)
        {
            return;
        }

        _currentMarkdown = newText;

        _parseCancellation?.Cancel();
        _parseCancellation?.Dispose();

        var cts = new CancellationTokenSource();
        _parseCancellation = cts;
        _ = ParseAndApplyAsync(newText, change, cts);
    }

    private async Task ParseAndApplyAsync(string markdown, MarkdownTextChange change, CancellationTokenSource cts)
    {
        var token = cts.Token;
        MarkdownDocument? document = null;

        try
        {
            document = _pipeline.Parse(markdown.AsSpan());
            token.ThrowIfCancellationRequested();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!ReferenceEquals(_parseCancellation, cts) || token.IsCancellationRequested)
                {
                    document?.Dispose();
                    return;
                }

                _parseCancellation?.Dispose();
                _parseCancellation = null;

                ApplyParsedDocument(document!, change, markdown.Length);
                document = null;
            }, DispatcherPriority.Render);
        }
        catch (OperationCanceledException)
        {
            document?.Dispose();
        }
        catch
        {
            document?.Dispose();
        }
        finally
        {
            document?.Dispose();
        }
    }

    private void ApplyParsedDocument(MarkdownDocument document, MarkdownTextChange change, int newLength)
    {
        var previous = _document;
        _presenter.NotifyTextChanged(change, newLength);
        _document = document;
        _presenter.Document = document;
        previous?.Dispose();
    }

    private static IServiceProvider CreateDefaultProvider()
    {
        var services = new ServiceCollection();
        services.AddSkiaMarkdownRendering();
        return services.BuildServiceProvider();
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (_presenter is null)
        {
            return;
        }

        Point? position = null;
        if (e.TryGetPosition(_presenter, out var point))
        {
            position = point;
        }

        _lastContextPoint = position;
        var linkUnderPointer = position.HasValue ? _presenter.GetLinkAt(position.Value) : null;
        var selectedLink = _presenter.GetFirstSelectedLink();
        _pendingContextLink = selectedLink ?? linkUnderPointer;

        _copyMenuItem.IsEnabled = _presenter.HasSelection || !string.IsNullOrEmpty(_presenter.GetDocumentText());
        _copyMarkdownMenuItem.IsEnabled = !string.IsNullOrEmpty(Markdown);
        _selectAllMenuItem.IsEnabled = true;
        var hasLink = !string.IsNullOrEmpty(_pendingContextLink);
        _openLinkMenuItem.IsEnabled = hasLink;
        _copyLinkMenuItem.IsEnabled = hasLink;

        _contextFlyout.ShowAt(this);
        e.Handled = true;
    }

    private async void OnCopyMenuItemClick(object? sender, RoutedEventArgs e) =>
        await CopySelectionAsync();

    private async void OnCopyMarkdownMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Markdown))
        {
            return;
        }

        await SetClipboardTextAsync(Markdown);
    }

    private void OnSelectAllMenuItemClick(object? sender, RoutedEventArgs e) => _presenter.SelectAll();

    private void OnOpenLinkMenuItemClick(object? sender, RoutedEventArgs e) =>
        OpenLink(_pendingContextLink ?? _presenter.GetFirstSelectedLink());

    private async void OnCopyLinkMenuItemClick(object? sender, RoutedEventArgs e)
    {
        var link = _pendingContextLink ?? _presenter.GetFirstSelectedLink();
        if (string.IsNullOrEmpty(link))
        {
            return;
        }

        await SetClipboardTextAsync(link);
        _pendingContextLink = null;
    }

    private async Task CopySelectionAsync()
    {
        var text = _presenter.HasSelection
            ? _presenter.GetSelectedText()
            : _presenter.GetDocumentText();

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await SetClipboardTextAsync(text);
    }

    private async Task SetClipboardTextAsync(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            return;
        }

        await topLevel.Clipboard.SetTextAsync(text);
    }

    private void OpenLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return;
        }

        var args = new MarkdownLinkEventArgs(link);
        LinkClicked?.Invoke(this, args);

        try
        {
            Process.Start(new ProcessStartInfo(link)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Opening a link is best-effort; ignore platform errors.
        }

        _pendingContextLink = null;
    }

    private void OnPresenterLinkClicked(object? sender, MarkdownLinkEventArgs e) => OpenLink(e.Link);

    internal string GetPlainText() => _presenter.GetDocumentText();
}
