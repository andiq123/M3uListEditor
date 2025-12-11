namespace Core.Interfaces;

/// <summary>
/// Tests if network links are alive/accessible.
/// </summary>
public interface ISignalTester
{
    /// <summary>
    /// Checks if the specified link responds successfully.
    /// </summary>
    /// <param name="link">The URL to test.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the link is accessible, false otherwise.</returns>
    Task<bool> IsLinkAliveAsync(string link, CancellationToken cancellationToken = default);
}
