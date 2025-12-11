using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Removes duplicate channels from a collection.
/// </summary>
public interface IDuplicateRemover
{
    /// <summary>
    /// Removes duplicate channels based on link.
    /// </summary>
    /// <param name="channels">The channels to deduplicate.</param>
    /// <returns>Result containing unique channels and removed count.</returns>
    DuplicateRemovalResult RemoveDuplicates(IReadOnlyList<Channel> channels);
}

/// <summary>
/// Result of duplicate removal operation.
/// </summary>
/// <param name="Channels">The deduplicated channels.</param>
/// <param name="RemovedCount">Number of duplicates removed.</param>
public record DuplicateRemovalResult(IReadOnlyList<Channel> Channels, int RemovedCount);
