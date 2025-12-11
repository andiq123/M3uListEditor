namespace Core.Models;

/// <summary>
/// Final report after processing channels.
/// </summary>
/// <param name="WorkingChannelsCount">Number of channels that are working.</param>
/// <param name="TotalChannelsCount">Total number of channels processed.</param>
/// <param name="DoublesRemovedCount">Number of duplicate channels removed.</param>
public record FinalChannelReport(
    int WorkingChannelsCount,
    int TotalChannelsCount,
    int DoublesRemovedCount);