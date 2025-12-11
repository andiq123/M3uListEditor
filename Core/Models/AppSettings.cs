namespace Core.Models;

/// <summary>
/// Application arguments/configuration.
/// </summary>
/// <param name="SourcePath">Path to the source M3U file or URL.</param>
/// <param name="ExportPath">Path to export the cleaned M3U file.</param>
/// <param name="Timeout">Timeout for HTTP requests.</param>
/// <param name="RemoveDoubles">Whether to remove duplicate channels.</param>
/// <param name="IsLinkSourcePath">Whether the source is a URL (not a local file).</param>
/// <param name="MaxConcurrency">Maximum concurrent HTTP requests for channel testing.</param>
public record AppSettings(
    string? SourcePath = null,
    string? ExportPath = null,
    TimeSpan? Timeout = null,
    bool RemoveDoubles = true,
    bool IsLinkSourcePath = false,
    int MaxConcurrency = 10)
{
    /// <summary>
    /// Default timeout of 10 seconds.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the effective timeout (uses default if not specified).
    /// </summary>
    public TimeSpan EffectiveTimeout => Timeout ?? DefaultTimeout;
}
