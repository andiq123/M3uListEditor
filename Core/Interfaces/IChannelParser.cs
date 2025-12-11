using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Parses M3U playlist files into channel collections.
/// </summary>
public interface IChannelParser
{
    /// <summary>
    /// Parses an M3U file and returns the channels.
    /// </summary>
    /// <param name="filePath">Path to the M3U file.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Collection of parsed channels.</returns>
    Task<IReadOnlyList<Channel>> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}
