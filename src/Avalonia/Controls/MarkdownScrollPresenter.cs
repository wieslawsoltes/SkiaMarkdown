using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaMarkdown.Core.Syntax;
using SkiaMarkdown.Rendering;
using SkiaMarkdown.Rendering.Layout;
using SkiaMarkdown.Rendering.Style;
using SkiaSharp;

namespace SkiaMarkdown.Avalonia.Controls;

/// <summary>
/// Scroll presenter that virtualizes Markdown rendering using Skia surfaces.
/// </summary>
public class MarkdownScrollPresenter : Control, ILogicalScrollable
{
    public static readonly StyledProperty<MarkdownDocument?> DocumentProperty =
        AvaloniaProperty.Register<MarkdownScrollPresenter, MarkdownDocument?>(nameof(Document));

    public static readonly StyledProperty<LayoutOptions> LayoutOptionsProperty =
        AvaloniaProperty.Register<MarkdownScrollPresenter, LayoutOptions>(nameof(LayoutOptions), LayoutOptions.Default);

    private static readonly Cursor? HandCursor = TryCreateCursor(StandardCursorType.Hand);

    private readonly MarkdownRenderer _renderer;
    private DocumentLayout? _layout;
    private LayoutOptions _layoutOptions = LayoutOptions.Default;
    private Size _extent;
    private Size _viewport;
    private Vector _offset = Vector.Zero;
    private float _layoutWidth = -1f;
    private bool _layoutDirty = true;
    private EventHandler? _scrollInvalidated;
    private bool _canHorizontallyScroll;
    private bool _canVerticallyScroll = true;
    private MarkdownTextChange _pendingTextChange = MarkdownTextChange.None;
    private int _pendingDocumentLength;
    private readonly List<TextRunLayout> _textRuns = new();
    private readonly HashSet<TextRunLayout> _selectedRuns = new();
    private TextRunLayout? _selectionAnchor;
    private TextRunLayout? _selectionActive;
    private TextRunLayout? _pressedRun;
    private bool _selectionChangedDuringDrag;

    private static Cursor? TryCreateCursor(StandardCursorType cursorType)
    {
        try
        {
            return new Cursor(cursorType);
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"Unable to create {cursorType} cursor: {ex.Message}");
            return null;
        }
    }

    static MarkdownScrollPresenter()
    {
        AffectsMeasure<MarkdownScrollPresenter>(DocumentProperty, LayoutOptionsProperty);
        AffectsRender<MarkdownScrollPresenter>(DocumentProperty, LayoutOptionsProperty);
    }

    public MarkdownScrollPresenter(MarkdownRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        ClipToBounds = true;
        Focusable = true;
        IsTabStop = true;
    }

    public event EventHandler<MarkdownLinkEventArgs>? LinkClicked;

    public MarkdownDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public LayoutOptions LayoutOptions
    {
        get => GetValue(LayoutOptionsProperty);
        set => SetValue(LayoutOptionsProperty, value);
    }

    public bool HasSelection => _selectedRuns.Count > 0;

    public void ClearSelection()
    {
        if (_selectedRuns.Count == 0)
        {
            return;
        }

        ClearSelectionInternal();
        InvalidateVisual();
    }

    public void SelectAll()
    {
        _selectedRuns.Clear();
        _selectedRuns.UnionWith(_textRuns);

        if (_textRuns.Count > 0)
        {
            _selectionAnchor = _textRuns[0];
            _selectionActive = _textRuns[^1];
        }

        InvalidateVisual();
    }

    public string GetSelectedText()
    {
        if (_selectedRuns.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, _textRuns.Where(_selectedRuns.Contains).Select(r => r.Text));
    }

    public string GetDocumentText() =>
        _textRuns.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, _textRuns.Select(r => r.Text));

    public string? GetFirstSelectedLink() =>
        _textRuns.FirstOrDefault(r => _selectedRuns.Contains(r) && !string.IsNullOrEmpty(r.LinkDestination))?.LinkDestination;

    public string? GetLinkAt(Point point)
    {
        var hit = HitTestTextRun(point);
        return string.IsNullOrEmpty(hit?.LinkDestination) ? null : hit!.LinkDestination;
    }

    /// <summary>
    /// Informs the presenter that the markdown text changed with the specified diff.
    /// </summary>
    /// <param name="change">The text change.</param>
    /// <param name="newDocumentLength">The length of the new markdown buffer.</param>
    public void NotifyTextChanged(MarkdownTextChange change, int newDocumentLength)
    {
        if (!change.HasChanges)
        {
            _pendingTextChange = MarkdownTextChange.None;
            _pendingDocumentLength = 0;
            return;
        }

        _pendingTextChange = change;
        _pendingDocumentLength = Math.Max(0, newDocumentLength);
    }

    bool ILogicalScrollable.CanHorizontallyScroll
    {
        get => _canHorizontallyScroll;
        set => _canHorizontallyScroll = value;
    }

    bool ILogicalScrollable.CanVerticallyScroll
    {
        get => _canVerticallyScroll;
        set => _canVerticallyScroll = value;
    }

    bool ILogicalScrollable.IsLogicalScrollEnabled => true;

    Size ILogicalScrollable.ScrollSize => new(
        0,
        Math.Max(1, _viewport.Height > 0 ? _viewport.Height / 10 : 24));

    Size ILogicalScrollable.PageScrollSize => new(
        0,
        Math.Max(1, _viewport.Height > 0 ? _viewport.Height * 0.9 : 240));

    Size IScrollable.Extent => _extent;

    Vector IScrollable.Offset
    {
        get => _offset;
        set => SetOffset(value, true);
    }

    Size IScrollable.Viewport => _viewport;

    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add => _scrollInvalidated += value;
        remove => _scrollInvalidated -= value;
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect) => false;

    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from) => null;

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e) => _scrollInvalidated?.Invoke(this, e);

    protected override Size MeasureOverride(Size availableSize)
    {
        var canvasWidth = GetCanvasWidth(availableSize.Width);
        EnsureLayout(canvasWidth);

        var height = _extent.Height;
        var desiredHeight = double.IsInfinity(availableSize.Height) ? height : Math.Min(height, availableSize.Height);

        return new Size(canvasWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var canvasWidth = GetCanvasWidth(finalSize.Width);
        var viewportChanged = _viewport != finalSize;
        if (viewportChanged)
        {
            _viewport = finalSize;
        }

        EnsureLayout(canvasWidth);
        var offsetChanged = SetOffset(_offset, false);

        if (viewportChanged || offsetChanged)
        {
            NotifyScrollChanged();
        }

        return base.ArrangeOverride(finalSize);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));

        if (_layout is null || _viewport.Width <= 0 || _viewport.Height <= 0)
        {
            return;
        }

        var width = Math.Max(1, (int)Math.Ceiling(_viewport.Width));
        var height = Math.Max(1, (int)Math.Ceiling(_viewport.Height));

        using var surface = _renderer.CreateSurface(new SKSizeI(width, height));
        var viewportRect = new SKRect(
            (float)_offset.X,
            (float)_offset.Y,
            (float)(_offset.X + _viewport.Width),
            (float)(_offset.Y + _viewport.Height));

        _renderer.RenderLayoutAsync(_layout, surface.Canvas, _layoutOptions, viewportRect, _selectedRuns)
            .GetAwaiter()
            .GetResult();
        surface.Canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 80);
        using var stream = data.AsStream();
        using var bitmap = new Bitmap(stream);

        var destRect = new Rect(0, 0, _viewport.Width, _viewport.Height);
        var sourceRect = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        context.DrawImage(bitmap, sourceRect, destRect);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_layout is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();

        var hit = HitTestTextRun(point.Position);
        _pressedRun = hit;
        _selectionChangedDuringDrag = false;

        if ((e.KeyModifiers & KeyModifiers.Shift) != 0 && _selectionAnchor is not null)
        {
            UpdateSelectionRange(_selectionAnchor, hit ?? _selectionAnchor);
        }
        else if (hit is not null)
        {
            UpdateSelectionRange(hit, hit);
        }
        else
        {
            ClearSelection();
        }

        _selectionActive = hit;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);
        var hit = HitTestTextRun(position);

        if (e.Pointer.Captured == this)
        {
            if (_selectionAnchor is not null)
            {
                var active = hit ?? _selectionAnchor;
                if (!ReferenceEquals(active, _selectionActive))
                {
                    _selectionChangedDuringDrag = true;
                    UpdateSelectionRange(_selectionAnchor, active);
                }
            }
            e.Handled = true;
            return;
        }

        UpdateCursor(hit);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        var point = e.GetCurrentPoint(this);
        if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
        {
            var hit = HitTestTextRun(point.Position);
            if (!_selectionChangedDuringDrag && hit is not null && ReferenceEquals(hit, _pressedRun) && !string.IsNullOrEmpty(hit.LinkDestination))
            {
                LinkClicked?.Invoke(this, new MarkdownLinkEventArgs(hit.LinkDestination!));
            }
        }

        _pressedRun = null;
        _selectionChangedDuringDrag = false;
        UpdateCursor(null);
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (e.Pointer.Captured != this)
        {
            UpdateCursor(null);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DocumentProperty)
        {
            if (change.GetNewValue<MarkdownDocument?>() is null)
            {
                _pendingTextChange = MarkdownTextChange.None;
                _pendingDocumentLength = 0;
                SetOffset(Vector.Zero, false);
            }

            InvalidateLayout();
        }
        else if (change.Property == LayoutOptionsProperty)
        {
            InvalidateLayout();
        }
    }

    private void InvalidateLayout()
    {
        _layoutDirty = true;
        _layout = null;
        _layoutWidth = -1f;
        _textRuns.Clear();
        ClearSelectionInternal();
        NotifyScrollChanged();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EnsureLayout(float canvasWidth)
    {
        canvasWidth = MathF.Max(1f, canvasWidth);

        if (!_layoutDirty && Math.Abs(_layoutWidth - canvasWidth) < 0.5f)
        {
            return;
        }

        var document = Document;
        _layoutWidth = canvasWidth;

        if (document is null)
        {
            var resetExtentChanged = UpdateExtent(new Size(canvasWidth, 0));
            _layout = null;
            _layoutDirty = false;
            var offsetChanged = SetOffset(Vector.Zero, false);
            if (resetExtentChanged || offsetChanged)
            {
                NotifyScrollChanged();
            }

            InvalidateVisual();
            return;
        }

        var options = LayoutOptions;
        var effectiveOptions = options with { CanvasWidth = canvasWidth };
        _layout = _renderer.BuildLayout(document, effectiveOptions);
        _layoutOptions = effectiveOptions;
        _layoutDirty = false;
        UpdateTextRuns();

        var layoutHeight = _layout.Bounds.Height;
        var layoutExtentChanged = UpdateExtent(new Size(canvasWidth, layoutHeight));
        var layoutOffsetChanged = SetOffset(_offset, false);

        if (layoutExtentChanged || layoutOffsetChanged)
        {
            NotifyScrollChanged();
        }

        ApplyPendingTextChange();
        InvalidateVisual();
    }

    private float GetCanvasWidth(double availableWidth)
    {
        if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) || availableWidth <= 0)
        {
            return MathF.Max(1f, LayoutOptions.CanvasWidth);
        }

        return (float)Math.Max(1d, availableWidth);
    }

    private bool SetOffset(Vector value, bool raiseEvent)
    {
        var clamped = ClampOffset(value);
        if (_offset == clamped)
        {
            return false;
        }

        _offset = clamped;
        InvalidateVisual();

        if (raiseEvent)
        {
            NotifyScrollChanged();
        }

        return true;
    }

    private Vector ClampOffset(Vector value)
    {
        var maxX = Math.Max(0, _extent.Width - _viewport.Width);
        var maxY = Math.Max(0, _extent.Height - _viewport.Height);
        var x = Math.Clamp(value.X, 0, maxX);
        var y = Math.Clamp(value.Y, 0, maxY);
        return new Vector(x, y);
    }

    private bool UpdateExtent(Size extent)
    {
        if (_extent == extent)
        {
            return false;
        }

        _extent = extent;
        return true;
    }

    private void NotifyScrollChanged() => _scrollInvalidated?.Invoke(this, EventArgs.Empty);

    private TextRunLayout? HitTestTextRun(Point point)
    {
        if (_layout is null || _textRuns.Count == 0)
        {
            return null;
        }

        var contentX = (float)(point.X + _offset.X);
        var contentY = (float)(point.Y + _offset.Y);

        foreach (var run in _textRuns)
        {
            var bounds = run.Bounds;
            if (contentX >= bounds.Left && contentX <= bounds.Right &&
                contentY >= bounds.Top && contentY <= bounds.Bottom)
            {
                return run;
            }
        }

        return null;
    }

    private void UpdateSelectionRange(TextRunLayout? anchor, TextRunLayout? active)
    {
        _selectedRuns.Clear();

        if (anchor is null || active is null)
        {
            _selectionAnchor = anchor;
            _selectionActive = active;
            InvalidateVisual();
            return;
        }

        var anchorIndex = _textRuns.IndexOf(anchor);
        var activeIndex = _textRuns.IndexOf(active);
        if (anchorIndex < 0 || activeIndex < 0)
        {
            InvalidateVisual();
            return;
        }

        if (anchorIndex > activeIndex)
        {
            (anchorIndex, activeIndex) = (activeIndex, anchorIndex);
        }

        for (var i = anchorIndex; i <= activeIndex; i++)
        {
            _selectedRuns.Add(_textRuns[i]);
        }

        _selectionAnchor = anchor;
        _selectionActive = active;
        InvalidateVisual();
    }

    private void UpdateCursor(TextRunLayout? hit)
    {
        Cursor = hit is not null && !string.IsNullOrEmpty(hit.LinkDestination)
            ? HandCursor
            : null;
    }

    private void ApplyPendingTextChange()
    {
        if (!_pendingTextChange.HasChanges || _pendingDocumentLength <= 0 || _extent.Height <= 0)
        {
            _pendingTextChange = MarkdownTextChange.None;
            _pendingDocumentLength = 0;
            return;
        }

        var change = _pendingTextChange;
        var documentLength = _pendingDocumentLength;
        _pendingTextChange = MarkdownTextChange.None;
        _pendingDocumentLength = 0;

        if (change.LengthDelta == 0 || documentLength <= 0)
        {
            return;
        }

        var totalHeight = _extent.Height;
        if (totalHeight <= 0)
        {
            return;
        }

        var changeRatio = Math.Clamp(change.Start / (double)documentLength, 0d, 1d);
        var topRatio = totalHeight <= 0 ? 0d : (_offset.Y / totalHeight);

        if (changeRatio <= topRatio)
        {
            var heightDelta = totalHeight * (change.LengthDelta / (double)documentLength);
            var newOffset = new Vector(_offset.X, _offset.Y + heightDelta);
            SetOffset(newOffset, true);
        }
    }

    private void UpdateTextRuns()
    {
        _textRuns.Clear();

        if (_layout is null)
        {
            return;
        }

        foreach (var run in _layout.EnumerateDrawables().OfType<TextRunLayout>())
        {
            _textRuns.Add(run);
        }

        ClearSelectionInternal();
    }

    private void ClearSelectionInternal()
    {
        _selectedRuns.Clear();
        _selectionAnchor = null;
        _selectionActive = null;
    }
}
