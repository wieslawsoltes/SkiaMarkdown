using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace SkiaMarkdown.Rendering;

/// <summary>
/// Provides no-op media resources, falling back to placeholders.
/// </summary>
internal sealed class NullMediaResourceProvider : IMediaResourceProvider
{
    public static NullMediaResourceProvider Instance { get; } = new();

    private NullMediaResourceProvider()
    {
    }

    public ValueTask<SKImage?> GetImageAsync(string source, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<SKImage?>(null);

    public ValueTask<SKImage?> GetVideoPosterAsync(string source, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<SKImage?>(null);

    public ValueTask<CustomDrawable?> GetCustomDrawableAsync(string identifier, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<CustomDrawable?>(null);
}
