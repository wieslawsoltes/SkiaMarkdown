using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaMarkdown.Core.Syntax;
using SkiaMarkdown.Rendering.Layout;
using SkiaMarkdown.Rendering.Style;
using SkiaSharp;

namespace SkiaMarkdown.Rendering;

/// <summary>
/// Coordinates rendering of Markdown documents onto Skia surfaces.
/// </summary>
public sealed class MarkdownRenderer : IDisposable
{
    private readonly ILogger<MarkdownRenderer> _logger;
    private readonly LayoutEngine _layoutEngine;
    private readonly IMediaResourceProvider _mediaProvider;
    private readonly RenderCache _renderCache;
    private readonly SKPaint _textPaint;
    private readonly SKPaint _fillPaint;
    private readonly SKPaint _strokePaint;
    private readonly SKPaint _dividerPaint;
    private bool _disposed;

    public MarkdownRenderer(ILogger<MarkdownRenderer>? logger = null, IMediaResourceProvider? mediaProvider = null)
    {
        _logger = logger ?? NullLogger<MarkdownRenderer>.Instance;
        _layoutEngine = new LayoutEngine();
        _mediaProvider = mediaProvider ?? NullMediaResourceProvider.Instance;
        _renderCache = new RenderCache();

        _textPaint = new SKPaint
        {
            IsAntialias = true
        };

        _fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _dividerPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };
    }

    public void Render(MarkdownDocument document, SKCanvas canvas, LayoutOptions? options = null, IReadOnlyCollection<TextRunLayout>? selection = null)
    {
        RenderAsync(document, canvas, options, selection).GetAwaiter().GetResult();
    }

    public Task RenderAsync(
        MarkdownDocument document,
        SKCanvas canvas,
        LayoutOptions? options = null,
        IReadOnlyCollection<TextRunLayout>? selection = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(canvas);

        return RenderInternalAsync(document, canvas, options, viewport: null, selection, cancellationToken);
    }

    public Task RenderViewportAsync(
        MarkdownDocument document,
        SKCanvas canvas,
        SKRect viewport,
        LayoutOptions? options = null,
        IReadOnlyCollection<TextRunLayout>? selection = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(canvas);

        if (viewport.IsEmpty)
        {
            throw new ArgumentException("Viewport must have a non-zero size.", nameof(viewport));
        }

        return RenderInternalAsync(document, canvas, options, viewport, selection, cancellationToken);
    }

    public DocumentLayout BuildLayout(MarkdownDocument document, LayoutOptions? options = null)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(document);

        var layoutOptions = options ?? LayoutOptions.Default;
        return _layoutEngine.Build(document, layoutOptions);
    }

    public Task RenderLayoutAsync(
        DocumentLayout layout,
        SKCanvas canvas,
        LayoutOptions? options = null,
        SKRect? viewport = null,
        IReadOnlyCollection<TextRunLayout>? selection = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(canvas);

        if (viewport is { IsEmpty: true })
        {
            throw new ArgumentException("Viewport must have a non-zero size.", nameof(viewport));
        }

        var layoutOptions = options ?? LayoutOptions.Default;
        var drawables = layout.EnumerateDrawables().ToList();
        return RenderLayoutInternalAsync(layoutOptions, drawables, canvas, viewport, selection, cancellationToken, blockCount: null);
    }

    public SKSurface CreateSurface(
        SKSizeI size,
        GRContext? gpuContext = null,
        bool budgeted = true,
        SKSurfaceProperties? properties = null,
        int sampleCount = 0)
    {
        ThrowIfDisposed();

        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        SKSurface? surface;

        if (gpuContext is null)
        {
            surface = SKSurface.Create(info);
        }
        else
        {
            surface = SKSurface.Create(gpuContext, budgeted, info, sampleCount, properties);
        }

        if (surface is null)
        {
            throw new InvalidOperationException("Unable to allocate an SKSurface for rendering.");
        }

        return surface;
    }

    public SKImage RenderToImage(
        MarkdownDocument document,
        SKSizeI size,
        LayoutOptions? options = null,
        GRContext? gpuContext = null,
        bool budgeted = true,
        SKSurfaceProperties? properties = null,
        int sampleCount = 0)
    {
        ThrowIfDisposed();

        using var surface = CreateSurface(size, gpuContext, budgeted, properties, sampleCount);
        RenderAsync(document, surface.Canvas, options, selection: null).GetAwaiter().GetResult();
        surface.Canvas.Flush();
        return surface.Snapshot();
    }

    public async Task<SKImage> RenderToImageAsync(
        MarkdownDocument document,
        SKSizeI size,
        LayoutOptions? options = null,
        GRContext? gpuContext = null,
        bool budgeted = true,
        SKSurfaceProperties? properties = null,
        int sampleCount = 0,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var surface = CreateSurface(size, gpuContext, budgeted, properties, sampleCount);
        await RenderAsync(document, surface.Canvas, options, selection: null, cancellationToken).ConfigureAwait(false);
        surface.Canvas.Flush();
        return surface.Snapshot();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _renderCache.Dispose();
        _textPaint.Dispose();
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _dividerPaint.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MarkdownRenderer));
        }
    }

    private Task RenderInternalAsync(
        MarkdownDocument document,
        SKCanvas canvas,
        LayoutOptions? options,
        SKRect? viewport,
        IReadOnlyCollection<TextRunLayout>? selection,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var clipWidth = viewport?.Width ?? canvas.DeviceClipBounds.Width;
        var layoutOptions = (options ?? LayoutOptions.Default) with
        {
            CanvasWidth = clipWidth
        };

        var layout = _layoutEngine.Build(document, layoutOptions);
        var drawables = layout.EnumerateDrawables().ToList();
        return RenderLayoutInternalAsync(layoutOptions, drawables, canvas, viewport, selection, cancellationToken, document.Blocks.Count);
    }

    private async Task RenderLayoutInternalAsync(
        LayoutOptions layoutOptions,
        IReadOnlyList<LayoutElement> drawables,
        SKCanvas canvas,
        SKRect? viewport,
        IReadOnlyCollection<TextRunLayout>? selection,
        CancellationToken cancellationToken,
        int? blockCount)
    {
        var effectiveDrawables = viewport is null
            ? drawables
            : FilterForViewport(drawables, viewport.Value);

        var mediaResources = await LoadMediaResourcesAsync(effectiveDrawables, cancellationToken).ConfigureAwait(false);
        var selectionSet = selection is null ? null : new HashSet<TextRunLayout>(selection);

        canvas.Clear(layoutOptions.Style.BackgroundColor);

        var translated = false;
        if (viewport is { } view)
        {
            canvas.Save();
            canvas.Translate(-view.Left, -view.Top);
            translated = true;
        }

        foreach (var element in effectiveDrawables)
        {
            switch (element)
            {
                case RectangleLayout rectangle:
                    DrawRectangle(canvas, rectangle);
                    break;
                case DividerLayout divider:
                    DrawDivider(canvas, divider);
                    break;
                case TextRunLayout textRun:
                    if (selectionSet is not null && selectionSet.Contains(textRun))
                    {
                        DrawSelectionHighlight(canvas, textRun, layoutOptions.Style);
                    }

                    DrawTextRun(canvas, textRun, layoutOptions.Style);
                    break;
                case ImageLayout imageLayout:
                    mediaResources.Images.TryGetValue(imageLayout, out var image);
                    DrawImage(canvas, imageLayout, image, layoutOptions.Style);
                    break;
                case VideoPlaceholderLayout videoLayout:
                    mediaResources.VideoPosters.TryGetValue(videoLayout, out var poster);
                    DrawVideoPlaceholder(canvas, videoLayout, poster, layoutOptions.Style);
                    break;
                case CustomDrawableLayout customLayout:
                    if (mediaResources.CustomDrawables.TryGetValue(customLayout, out var drawable) && drawable is not null)
                    {
                        drawable.Draw(canvas, customLayout.Bounds);
                    }
                    else
                    {
                        DrawCustomPlaceholder(canvas, customLayout, layoutOptions.Style);
                    }
                    break;
            }
        }

        if (translated)
        {
            canvas.Restore();
        }

        if (blockCount is not null)
        {
            _logger.LogDebug("Rendered document with {BlockCount} blocks.", blockCount.Value);
        }
    }

    private static IReadOnlyList<LayoutElement> FilterForViewport(IReadOnlyList<LayoutElement> drawables, SKRect viewport)
    {
        if (drawables.Count == 0)
        {
            return Array.Empty<LayoutElement>();
        }

        var results = new List<LayoutElement>();
        foreach (var element in drawables)
        {
            if (Intersects(element.Bounds, viewport))
            {
                results.Add(element);
            }
        }

        return results;
    }

    private static bool Intersects(SKRect a, SKRect b) =>
        a.Left < b.Right &&
        a.Right > b.Left &&
        a.Top < b.Bottom &&
        a.Bottom > b.Top;

    private void DrawRectangle(SKCanvas canvas, RectangleLayout rectangle)
    {
        _fillPaint.Color = rectangle.FillColor;

        if (rectangle.CornerRadius > 0)
        {
            using var path = new SKPath();
            path.AddRoundRect(rectangle.Bounds, rectangle.CornerRadius, rectangle.CornerRadius);
            canvas.DrawPath(path, _fillPaint);

            if (rectangle.StrokeColor.HasValue && rectangle.StrokeThickness > 0)
            {
                _strokePaint.Color = rectangle.StrokeColor.Value;
                _strokePaint.StrokeWidth = rectangle.StrokeThickness;
                canvas.DrawPath(path, _strokePaint);
            }
        }
        else
        {
            canvas.DrawRect(rectangle.Bounds, _fillPaint);

            if (rectangle.StrokeColor.HasValue && rectangle.StrokeThickness > 0)
            {
                _strokePaint.Color = rectangle.StrokeColor.Value;
                _strokePaint.StrokeWidth = rectangle.StrokeThickness;
                canvas.DrawRect(rectangle.Bounds, _strokePaint);
            }
        }
    }

    private void DrawDivider(SKCanvas canvas, DividerLayout divider)
    {
        _dividerPaint.Color = divider.Color;
        _dividerPaint.StrokeWidth = divider.Thickness;
        canvas.DrawLine(divider.Bounds.Left, divider.Bounds.Top, divider.Bounds.Right, divider.Bounds.Top, _dividerPaint);
    }

    private void DrawTextRun(SKCanvas canvas, TextRunLayout textRun, RendererStyle style)
    {
        if (string.IsNullOrEmpty(textRun.Text))
        {
            return;
        }

        var typeface = textRun.Typeface ?? style.Body.Typeface;

        _textPaint.Typeface = typeface;
        _textPaint.TextSize = textRun.FontSize;
        var isLink = !string.IsNullOrEmpty(textRun.LinkDestination);
        _textPaint.Color = isLink
            ? new SKColor(0x0, 0x66, 0xcc)
            : textRun.Color;

        var blob = _renderCache.GetOrAddTextBlob(textRun.Text, textRun.FontSize, typeface);
        if (blob is not null)
        {
            canvas.DrawText(blob, textRun.Bounds.Left, textRun.Baseline, _textPaint);
        }
        else
        {
            canvas.DrawText(textRun.Text, textRun.Bounds.Left, textRun.Baseline, _textPaint);
        }

        if (isLink)
        {
            var underlineY = textRun.Baseline + 2f;
            using var underlinePaint = new SKPaint
            {
                Color = _textPaint.Color,
                StrokeWidth = Math.Max(1f, textRun.FontSize * 0.05f),
                IsAntialias = true
            };
            canvas.DrawLine(textRun.Bounds.Left, underlineY, textRun.Bounds.Right, underlineY, underlinePaint);
        }
    }
    private void DrawSelectionHighlight(SKCanvas canvas, TextRunLayout textRun, RendererStyle style)
    {
        using var highlight = new SKPaint
        {
            Color = new SKColor(style.Body.Color.Red, style.Body.Color.Green, style.Body.Color.Blue, 60),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        canvas.DrawRect(textRun.Bounds, highlight);
    }

    private void DrawImage(SKCanvas canvas, ImageLayout layout, SKImage? image, RendererStyle style)
    {
        var media = style.Media;

        if (image is not null && image.Width > 0 && image.Height > 0)
        {
            var dest = layout.Bounds;
            var scale = Math.Min(dest.Width / image.Width, dest.Height / image.Height);
            var width = image.Width * scale;
            var height = image.Height * scale;
            var left = dest.Left + ((dest.Width - width) / 2f);
            var top = dest.Top + ((dest.Height - height) / 2f);
            var rect = new SKRect(left, top, left + width, top + height);
            canvas.DrawImage(image, rect);
            DrawMediaBorder(canvas, dest, media);
            return;
        }

        DrawMediaPlaceholder(canvas, layout.Bounds, layout.AlternativeText, media);
    }

    private void DrawVideoPlaceholder(SKCanvas canvas, VideoPlaceholderLayout layout, SKImage? poster, RendererStyle style)
    {
        var media = style.Media;

        if (poster is not null && poster.Width > 0 && poster.Height > 0)
        {
            var dest = layout.Bounds;
            var scale = Math.Min(dest.Width / poster.Width, dest.Height / poster.Height);
            var width = poster.Width * scale;
            var height = poster.Height * scale;
            var left = dest.Left + ((dest.Width - width) / 2f);
            var top = dest.Top + ((dest.Height - height) / 2f);
            var rect = new SKRect(left, top, left + width, top + height);
            canvas.DrawImage(poster, rect);
            DrawMediaBorder(canvas, dest, media);
        }
        else
        {
            DrawMediaPlaceholder(canvas, layout.Bounds, layout.Title, media);
        }

        DrawPlayIcon(canvas, layout.Bounds);
    }

    private void DrawCustomPlaceholder(SKCanvas canvas, CustomDrawableLayout layout, RendererStyle style)
    {
        var caption = layout.Metadata.TryGetValue("label", out var label)
            ? label
            : layout.Identifier;
        DrawMediaPlaceholder(canvas, layout.Bounds, caption, style.Media);
    }

    private void DrawMediaPlaceholder(SKCanvas canvas, SKRect bounds, string? caption, MediaStyle media)
    {
        _fillPaint.Color = media.PlaceholderBackground;

        if (media.CornerRadius > 0)
        {
            using var path = new SKPath();
            path.AddRoundRect(bounds, media.CornerRadius, media.CornerRadius);
            canvas.DrawPath(path, _fillPaint);
        }
        else
        {
            canvas.DrawRect(bounds, _fillPaint);
        }

        DrawMediaBorder(canvas, bounds, media);

        if (!string.IsNullOrWhiteSpace(caption))
        {
            _textPaint.Typeface = media.PlaceholderText.Typeface;
            _textPaint.TextSize = media.PlaceholderText.FontSize;
            _textPaint.Color = media.PlaceholderText.Color;

            var textWidth = _textPaint.MeasureText(caption);
            var metrics = _textPaint.FontMetrics;
            var textHeight = metrics.Descent - metrics.Ascent;
            var x = bounds.MidX - (textWidth / 2f);
            var y = bounds.MidY + (textHeight / 2f) - metrics.Descent;
            canvas.DrawText(caption, x, y, _textPaint);
        }
    }

    private void DrawMediaBorder(SKCanvas canvas, SKRect bounds, MediaStyle media)
    {
        if (media.PlaceholderBorder.Alpha == 0)
        {
            return;
        }

        _strokePaint.Color = media.PlaceholderBorder;
        _strokePaint.StrokeWidth = 1f;

        if (media.CornerRadius > 0)
        {
            using var path = new SKPath();
            path.AddRoundRect(bounds, media.CornerRadius, media.CornerRadius);
            canvas.DrawPath(path, _strokePaint);
        }
        else
        {
            canvas.DrawRect(bounds, _strokePaint);
        }
    }

    private void DrawPlayIcon(SKCanvas canvas, SKRect bounds)
    {
        var diameter = Math.Min(bounds.Width, bounds.Height) * 0.3f;
        var center = new SKPoint(bounds.MidX, bounds.MidY);

        var playPath = new SKPath();
        playPath.MoveTo(center.X - (diameter / 2f), center.Y - diameter);
        playPath.LineTo(center.X - (diameter / 2f), center.Y + diameter);
        playPath.LineTo(center.X + (diameter / 2f), center.Y);
        playPath.Close();

        using var fill = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 220),
            IsAntialias = true
        };

        using var stroke = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 180),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true
        };

        canvas.DrawPath(playPath, fill);
        canvas.DrawPath(playPath, stroke);
    }

    private async Task<MediaResources> LoadMediaResourcesAsync(IEnumerable<LayoutElement> drawables, CancellationToken cancellationToken)
    {
        var images = new Dictionary<ImageLayout, SKImage?>();
        var posters = new Dictionary<VideoPlaceholderLayout, SKImage?>();
        var customs = new Dictionary<CustomDrawableLayout, CustomDrawable?>();
        var imageCache = new Dictionary<string, SKImage?>(StringComparer.OrdinalIgnoreCase);

        foreach (var imageLayout in drawables.OfType<ImageLayout>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!imageCache.TryGetValue(imageLayout.Source, out var image))
            {
                image = await SafeLoadImageAsync(imageLayout.Source, cancellationToken).ConfigureAwait(false);
                imageCache[imageLayout.Source] = image;
            }

            images[imageLayout] = image;
        }

        foreach (var videoLayout in drawables.OfType<VideoPlaceholderLayout>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            SKImage? poster;

            if (!string.IsNullOrWhiteSpace(videoLayout.PosterSource))
            {
                var key = $"poster:{videoLayout.PosterSource}";
                if (!imageCache.TryGetValue(key, out poster))
                {
                    poster = await SafeLoadImageAsync(videoLayout.PosterSource!, cancellationToken).ConfigureAwait(false);
                    imageCache[key] = poster;
                }
            }
            else
            {
                poster = await SafeLoadVideoPosterAsync(videoLayout.Source, cancellationToken).ConfigureAwait(false);
            }

            posters[videoLayout] = poster;
        }

        foreach (var customLayout in drawables.OfType<CustomDrawableLayout>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var drawable = await SafeLoadCustomDrawableAsync(customLayout, cancellationToken).ConfigureAwait(false);
            customs[customLayout] = drawable;
        }

        return new MediaResources(images, posters, customs);
    }

    private async Task<SKImage?> SafeLoadImageAsync(string source, CancellationToken cancellationToken)
    {
        try
        {
            return await _mediaProvider.GetImageAsync(source, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load image resource '{Source}'.", source);
            return null;
        }
    }

    private async Task<SKImage?> SafeLoadVideoPosterAsync(string source, CancellationToken cancellationToken)
    {
        try
        {
            return await _mediaProvider.GetVideoPosterAsync(source, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load video poster '{Source}'.", source);
            return null;
        }
    }

    private async Task<CustomDrawable?> SafeLoadCustomDrawableAsync(CustomDrawableLayout layout, CancellationToken cancellationToken)
    {
        try
        {
            return await _mediaProvider.GetCustomDrawableAsync(layout.Identifier, layout.Metadata, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load custom drawable '{Identifier}'.", layout.Identifier);
            return null;
        }
    }

    private sealed record MediaResources(
        IReadOnlyDictionary<ImageLayout, SKImage?> Images,
        IReadOnlyDictionary<VideoPlaceholderLayout, SKImage?> VideoPosters,
        IReadOnlyDictionary<CustomDrawableLayout, CustomDrawable?> CustomDrawables);
}
