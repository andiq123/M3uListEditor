using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Handles file system operations for M3U files.
/// </summary>
public interface IFileHandler
{
    /// <summary>
    /// Writes channels to an M3U file.
    /// </summary>
    void WriteChannels(string path, IEnumerable<Channel> channels);

    /// <summary>
    /// Writes channels to an M3U file asynchronously.
    /// </summary>
    Task WriteChannelsAsync(string path, IEnumerable<Channel> channels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file if it exists.
    /// </summary>
    void DeleteFileIfExists(string path);

    /// <summary>
    /// Creates a directory if it doesn't exist.
    /// </summary>
    void EnsureDirectoryExists(string path);
}
