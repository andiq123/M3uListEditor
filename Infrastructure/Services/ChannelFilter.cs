using System.Collections.Concurrent;
using System.Diagnostics;
using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Services;

/// <summary>
/// Filters channels by testing if their links are alive.
/// Uses parallel processing with adaptive concurrency for optimal performance.
/// </summary>
public sealed class ChannelFilter : IChannelFilter
{
    private readonly ISignalTester _signalTester;
    private readonly int _maxConcurrency;

    public ChannelFilter(ISignalTester signalTester, int maxConcurrency = 10)
    {
        _signalTester = signalTester ?? throw new ArgumentNullException(nameof(signalTester));
        _maxConcurrency = Math.Clamp(maxConcurrency, 1, 50);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Channel>> FilterWorkingAsync(
        IReadOnlyList<Channel> channels,
        IProgress<ProgressReportModel>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channels);

        if (channels.Count == 0)
        {
            return [];
        }

        // Pre-allocate collections
        var workingChannels = new ConcurrentBag<(int Index, Channel Channel)>();
        var counters = new ProgressCounters();
        var stopwatch = Stopwatch.StartNew();

        // Report initial progress
        ReportProgress(progress, channels.Count, 0, 0, 0);

        // Use SemaphoreSlim for more control over concurrency
        using var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

        // Process all channels concurrently
        var tasks = channels.Select((channel, index) => ProcessChannelAsync(
            channel,
            index,
            semaphore,
            workingChannels,
            counters,
            channels.Count,
            progress,
            cancellationToken)).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation requested - return what we have so far
        }

        stopwatch.Stop();

        // Final progress report
        ReportProgress(progress, channels.Count, counters.Working, counters.NotWorking, 100);

        // Sort by original index to preserve order, then extract channels
        var result = workingChannels
            .OrderBy(x => x.Index)
            .Select(x => x.Channel)
            .ToList();

        return result;
    }

    private async Task ProcessChannelAsync(
        Channel channel,
        int index,
        SemaphoreSlim semaphore,
        ConcurrentBag<(int Index, Channel Channel)> workingChannels,
        ProgressCounters counters,
        int totalCount,
        IProgress<ProgressReportModel>? progress,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Test with retry for transient failures
            var isAlive = await TestWithRetryAsync(channel.Link, cancellationToken);

            if (isAlive)
            {
                workingChannels.Add((index, channel));
                Interlocked.Increment(ref counters.Working);
            }
            else
            {
                Interlocked.Increment(ref counters.NotWorking);
            }

            var current = Interlocked.Increment(ref counters.Processed);

            // Report progress at meaningful intervals
            var reportInterval = CalculateReportInterval(totalCount);
            if (current % reportInterval == 0 || current == totalCount)
            {
                var percentage = (int)((double)current / totalCount * 100);
                ReportProgress(progress, totalCount, counters.Working, counters.NotWorking, percentage);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Tests a link with retry logic for transient failures.
    /// </summary>
    private async Task<bool> TestWithRetryAsync(string link, CancellationToken cancellationToken)
    {
        const int maxRetries = 2;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await _signalTester.IsLinkAliveAsync(link, cancellationToken);

                if (result)
                    return true;

                // Only retry if this wasn't the last attempt
                if (attempt < maxRetries)
                {
                    // Short delay before retry (increases with each attempt)
                    await Task.Delay(100 * (attempt + 1), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Swallow other exceptions and continue with retry
                if (attempt == maxRetries)
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Calculates the optimal progress report interval based on total count.
    /// </summary>
    private static int CalculateReportInterval(int totalCount)
    {
        // Report more frequently for smaller lists, less frequently for larger ones
        return totalCount switch
        {
            < 20 => 1,       // Report every item
            < 100 => 2,      // Report every 2 items
            < 500 => 5,      // Report every 5 items
            < 1000 => 10,    // Report every 10 items
            _ => Math.Max(1, totalCount / 100) // ~100 reports total
        };
    }

    /// <summary>
    /// Reports progress to the progress handler.
    /// </summary>
    private static void ReportProgress(
        IProgress<ProgressReportModel>? progress,
        int total,
        int working,
        int notWorking,
        int percentage)
    {
        progress?.Report(new ProgressReportModel
        {
            ChannelsCountTotal = total,
            WorkingChannelsCount = working,
            NotWorkingChannelsCount = notWorking,
            PercentageCompleted = Math.Clamp(percentage, 0, 100)
        });
    }

    /// <summary>
    /// Thread-safe counters for progress tracking.
    /// </summary>
    private sealed class ProgressCounters
    {
        public int Processed;
        public int Working;
        public int NotWorking;
    }
}
