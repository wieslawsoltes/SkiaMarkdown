namespace SkiaMarkdown.Core.Configuration;

/// <summary>
/// Represents immutable configuration for the Markdown parsing pipeline.
/// </summary>
public sealed record MarkdownOptions
{
    /// <summary>
    /// Gets the maximum size in bytes for a single Markdown document before streaming kicks in.
    /// </summary>
    public int StreamingThresholdBytes { get; init; } = 256 * 1024;

    /// <summary>
    /// Gets a value indicating whether raw HTML is allowed to flow through the pipeline.
    /// </summary>
    public bool AllowRawHtml { get; init; }

    /// <summary>
    /// Gets a value indicating whether GitHub Flavored Markdown extensions are enabled.
    /// </summary>
    public bool EnableGitHubExtensions { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether incremental parsing support is enabled.
    /// </summary>
    public bool EnableIncrementalParsing { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether telemetry events are emitted using <see cref="System.Diagnostics.Metrics"/>.
    /// </summary>
    public bool EnableTelemetry { get; init; } = true;
}
