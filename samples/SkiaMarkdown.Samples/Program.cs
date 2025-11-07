using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SkiaMarkdown.Avalonia.Controls;
using SkiaMarkdown.Core.Pipeline;
using SkiaMarkdown.Rendering;
using SkiaMarkdown.Rendering.DependencyInjection;
using SkiaSharp;

const string SampleMarkdown = """
# SkiaMarkdown Samples

- [x] Render GitHub flavored Markdown
- [ ] Integrate with Avalonia
- [ ] Harness SkiaSharp for GPU acceleration

![Architecture Overview](https://example.com/media/skia-markdown.png)

<video src="https://example.com/media/overview.mp4" title="Architecture Overview"></video>

::: drawable sparkline width=480 height=180 label="Render Stats"
:::
""";

var services = new ServiceCollection();
services.AddSkiaMarkdownRendering();
services.AddSingleton<IMediaResourceProvider, SampleMediaResourceProvider>();
using var provider = services.BuildServiceProvider();

var pipeline = provider.GetRequiredService<MarkdownPipeline>();
var renderer = provider.GetRequiredService<MarkdownRenderer>();
using var document = pipeline.Parse(SampleMarkdown.AsSpan());
Console.WriteLine($"Parsed sample document with {document.Blocks.Count} blocks.");

var view = new MarkdownView(provider)
{
    Width = 640,
    Height = 480,
    Markdown = SampleMarkdown
};

Console.WriteLine("MarkdownView initialized with sample content and ready for embedding.");

var targetSize = new SKSizeI(800, 600);
using var image = await renderer.RenderToImageAsync(document, targetSize);
using var data = image.Encode(SKEncodedImageFormat.Png, 90);
using var file = File.Open("skia-markdown-sample.png", FileMode.Create, FileAccess.Write);
data.SaveTo(file);

Console.WriteLine("Rendered sample Markdown to skia-markdown-sample.png");

public sealed class SampleMediaResourceProvider : IMediaResourceProvider, IDisposable
{
    private readonly Dictionary<string, SKImage?> _imageCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public ValueTask<SKImage?> GetImageAsync(string source, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SampleMediaResourceProvider));
        }

        if (_imageCache.TryGetValue(source, out var cached))
        {
            return ValueTask.FromResult(cached);
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.IsFile && File.Exists(uri.LocalPath))
        {
            using var data = SKData.Create(uri.LocalPath);
            if (data is not null)
            {
                var image = SKImage.FromEncodedData(data);
                _imageCache[source] = image;
                return ValueTask.FromResult<SKImage?>(image);
            }
        }

        _imageCache[source] = null;
        return ValueTask.FromResult<SKImage?>(null);
    }

    public ValueTask<SKImage?> GetVideoPosterAsync(string source, CancellationToken cancellationToken = default) =>
        GetImageAsync(source, cancellationToken);

    public ValueTask<CustomDrawable?> GetCustomDrawableAsync(string identifier, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SampleMediaResourceProvider));
        }

        if (!identifier.Equals("sparkline", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<CustomDrawable?>(null);
        }

        return ValueTask.FromResult<CustomDrawable?>(new CustomDrawable((canvas, bounds) =>
        {
            using var stroke = new SKPaint
            {
                Color = SKColors.DeepSkyBlue,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true
            };

            using var path = new SKPath();
            var steps = 12;
            for (var i = 0; i < steps; i++)
            {
                var x = bounds.Left + (i / (float)(steps - 1)) * bounds.Width;
                var normalized = (float)Math.Sin(i * 0.7f) * 0.4f + 0.5f;
                var y = bounds.Bottom - (normalized * bounds.Height);
                if (i == 0)
                {
                    path.MoveTo(x, y);
                }
                else
                {
                    path.LineTo(x, y);
                }
            }

            canvas.DrawPath(path, stroke);
        }));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var image in _imageCache.Values)
        {
            image?.Dispose();
        }

        _imageCache.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
