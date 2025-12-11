using System.Collections.Concurrent;
using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Services;

/// <summary>
/// Filters channels by testing if their links are alive using parallel processing.
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
            return [];

        var workingChannels = new ConcurrentBag<(int Index, Channel Channel)>();
        var counters = new ProgressCounters();

        ReportProgress(progress, channels.Count, 0, 0, 0);

        using var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

        var tasks = channels.Select((channel, index) => ProcessChannelAsync(
            channel, index, semaphore, workingChannels, counters,
            channels.Count, progress, cancellationToken)).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        ReportProgress(progress, channels.Count, counters.Working, counters.NotWorking, 100);

        return workingChannels
            .OrderBy(x => x.Index)
            .Select(x => x.Channel)
            .ToList();
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

    private async Task<bool> TestWithRetryAsync(string link, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= 2; attempt++)
        {
            try
            {
                var result = await _signalTester.IsLinkAliveAsync(link, cancellationToken);
                if (result) return true;
                if (attempt < 2)
                    await Task.Delay(100 * (attempt + 1), cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch { if (attempt == 2) return false; }
        }
        return false;
    }

    private static int CalculateReportInterval(int totalCount) => totalCount switch
    {
        < 20 => 1,
        < 100 => 2,
        < 500 => 5,
        < 1000 => 10,
        _ => Math.Max(1, totalCount / 100)
    };

    private static void ReportProgress(IProgress<ProgressReportModel>? progress, int total, int working, int notWorking, int percentage)
    {
        progress?.Report(new ProgressReportModel
        {
            ChannelsCountTotal = total,
            WorkingChannelsCount = working,
            NotWorkingChannelsCount = notWorking,
            PercentageCompleted = Math.Clamp(percentage, 0, 100)
        });
    }

    private sealed class ProgressCounters
    {
        public int Processed;
        public int Working;
        public int NotWorking;
    }
}
