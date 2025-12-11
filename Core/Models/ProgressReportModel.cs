namespace Core.Models;

/// <summary>
/// Reports progress during channel filtering.
/// </summary>
public record ProgressReportModel
{
    /// <summary>
    /// Percentage of channels processed (0-100).
    /// </summary>
    public int PercentageCompleted { get; init; }

    /// <summary>
    /// Total number of channels to process.
    /// </summary>
    public int ChannelsCountTotal { get; init; }

    /// <summary>
    /// Number of channels that passed testing.
    /// </summary>
    public int WorkingChannelsCount { get; init; }

    /// <summary>
    /// Number of channels that failed testing.
    /// </summary>
    public int NotWorkingChannelsCount { get; init; }

    // Removed: IReadOnlyList<Channel> WorkingChannels - was causing unnecessary allocations
}