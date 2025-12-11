using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Downloads files from remote URLs.
/// </summary>
public interface IFileDownloader
{
    /// <summary>
    /// Downloads a file from a URL and returns the local path.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Path to the downloaded file.</returns>
    Task<string> DownloadAsync(string url, CancellationToken cancellationToken = default);
}
