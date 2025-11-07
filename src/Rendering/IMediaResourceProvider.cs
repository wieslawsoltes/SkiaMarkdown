using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace SkiaMarkdown.Rendering;

/// <summary>
/// Provides media resources (images, video posters, custom drawables) to the renderer.
/// </summary>
public interface IMediaResourceProvider
{
    ValueTask<SKImage?> GetImageAsync(string source, CancellationToken cancellationToken = default);

    ValueTask<SKImage?> GetVideoPosterAsync(string source, CancellationToken cancellationToken = default);

    ValueTask<CustomDrawable?> GetCustomDrawableAsync(string identifier, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default);
}
