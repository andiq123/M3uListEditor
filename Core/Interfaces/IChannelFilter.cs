using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Filters channels based on various criteria.
/// </summary>
public interface IChannelFilter
{
    /// <summary>
    /// Filters channels to return only working ones.
    /// </summary>
    /// <param name="channels">The channels to filter.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Collection of working channels.</returns>
    Task<IReadOnlyList<Channel>> FilterWorkingAsync(
        IReadOnlyList<Channel> channels,
        IProgress<ProgressReportModel>? progress = null,
        CancellationToken cancellationToken = default);
}
