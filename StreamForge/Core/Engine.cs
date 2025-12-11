using System.Text;
using StreamForge.Services;

namespace StreamForge.Core;

/// <summary>
/// Main processing engine for M3U playlists.
/// </summary>
public sealed class Engine
{
    private readonly Parser _parser;
    private readonly Processor _processor;
    private readonly Validator _validator;
    private readonly Enhancer _enhancer;
    private readonly FileIO _fileIO;

    public Engine(HttpClient httpClient, int maxConcurrency = 10)
    {
        _parser = new Parser();
        _processor = new Processor();
        _validator = new Validator(httpClient, maxConcurrency);
        _enhancer = new Enhancer(httpClient);
        _fileIO = new FileIO(httpClient);
    }

    /// <summary>
    /// Downloads a playlist from URL.
    /// </summary>
    public Task<string> DownloadAsync(string url, CancellationToken cancellationToken = default)
        => _fileIO.DownloadAsync(url, cancellationToken);

    /// <summary>
    /// Processes M3U file(s) and creates cleaned version(s).
    /// </summary>
    public async Task<ProcessingReport> ProcessAsync(
        AppSettings settings,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var channels = await LoadChannelsAsync(settings, cancellationToken);
        var originalCount = channels.Count;

        progress?.Report(new ProgressReport { ChannelsCountTotal = originalCount, PercentageCompleted = 0, CurrentActivity = "Analyzing content..." });

        var doublesRemoved = 0;
        if (settings.RemoveDoubles)
        {
            progress?.Report(new ProgressReport { ChannelsCountTotal = originalCount, PercentageCompleted = 5, CurrentActivity = "Removing duplicates..." });
            var result = _processor.RemoveDuplicates(channels);
            channels = result.Channels;
            doublesRemoved = result.RemovedCount;
        }

        var categorizedCount = 0;
        var languagesCount = 0;

        var filterOptions = settings.ToFilterOptions();
        channels = _processor.Filter(channels, filterOptions);

        if (!string.IsNullOrEmpty(settings.RenamePattern))
        {
            progress?.Report(new ProgressReport { ChannelsCountTotal = channels.Count, PercentageCompleted = 10, CurrentActivity = "Renaming channels..." });
            channels = _enhancer.RenameChannels(channels, settings.RenamePattern, settings.RenameReplacement ?? "", settings.RenameUseRegex);
        }

        if (settings.AutoCategorize)
        {
            progress?.Report(new ProgressReport { ChannelsCountTotal = channels.Count, PercentageCompleted = 15, CurrentActivity = "Enhancing: Auto-categorizing..." });
            var before = channels.Count(c => c.Category != ChannelCategory.Unknown);
            channels = _enhancer.AutoCategorize(channels);
            categorizedCount = channels.Count(c => c.Category != ChannelCategory.Unknown) - before;
        }

        if (settings.DetectLanguage)
        {
            progress?.Report(new ProgressReport { ChannelsCountTotal = channels.Count, PercentageCompleted = 20, CurrentActivity = "Enhancing: Detecting languages..." });
            var before = channels.Count(c => c.Language != null);
            channels = _enhancer.DetectLanguage(channels);
            languagesCount = channels.Count(c => c.Language != null) - before;
        }

        if (settings.DetectContentDuplicates)
        {
            progress?.Report(new ProgressReport { ChannelsCountTotal = channels.Count, PercentageCompleted = 25, CurrentActivity = "Enhancing: Scanning content signatures..." });
            channels = await _enhancer.DetectDuplicateContentAsync(channels, progress, cancellationToken);
        }

        progress?.Report(new ProgressReport { ChannelsCountTotal = channels.Count, PercentageCompleted = 40, CurrentActivity = "Sorting channels..." });
        channels = _processor.Sort(channels, filterOptions);

        IReadOnlyList<Channel> workingChannels;
        if (settings.SkipValidation)
        {
            workingChannels = channels;
            progress?.Report(new ProgressReport
            {
                ChannelsCountTotal = channels.Count,
                WorkingChannelsCount = channels.Count,
                NotWorkingChannelsCount = 0,
                PercentageCompleted = 100,
                CurrentActivity = "Validation skipped."
            });
        }
        else
        {
            progress?.Report(new ProgressReport { ChannelsCountTotal = channels.Count, PercentageCompleted = 45, CurrentActivity = "Validating streams..." });

            var validationProgress = new Progress<ProgressReport>(report =>
            {
                progress?.Report(report with { CurrentActivity = $"Validating streams ({report.WorkingChannelsCount + report.NotWorkingChannelsCount}/{channels.Count})..." });
            });

            workingChannels = await _validator.FilterWorkingAsync(channels, validationProgress, cancellationToken);
        }

        if (settings.SplitByGroup && !string.IsNullOrEmpty(settings.ExportPath))
        {
            progress?.Report(new ProgressReport { ChannelsCountTotal = channels.Count, PercentageCompleted = 95, CurrentActivity = "Writing output files..." });
            await WriteSplitFilesAsync(workingChannels, settings.ExportPath, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(settings.ExportPath))
        {
            progress?.Report(new ProgressReport { ChannelsCountTotal = channels.Count, PercentageCompleted = 95, CurrentActivity = "Writing output file..." });
            await _fileIO.WriteChannelsAsync(settings.ExportPath, workingChannels, cancellationToken);
        }

        return new ProcessingReport(
            WorkingChannelsCount: workingChannels.Count,
            TotalChannelsCount: channels.Count,
            DoublesRemovedCount: doublesRemoved,
            OriginalCount: originalCount,
            GroupCount: _processor.GetGroups(workingChannels).Count,
            CategorizedCount: categorizedCount,
            LanguagesDetectedCount: languagesCount);
    }

    private async Task<IReadOnlyList<Channel>> LoadChannelsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        if (settings.MergeFiles && settings.SourcePaths.Count > 1)
        {
            var allChannels = new List<IReadOnlyList<Channel>>();
            foreach (var path in settings.SourcePaths)
            {
                var channels = await _parser.ParseAsync(path, cancellationToken);
                allChannels.Add(channels);
            }
            return _processor.Merge(allChannels.ToArray());
        }

        if (!string.IsNullOrEmpty(settings.SourcePath))
            return await _parser.ParseAsync(settings.SourcePath, cancellationToken);

        if (settings.SourcePaths.Count > 0)
            return await _parser.ParseAsync(settings.SourcePaths[0], cancellationToken);

        throw new ArgumentException("No source path specified.", nameof(settings));
    }

    private async Task WriteSplitFilesAsync(IReadOnlyList<Channel> channels, string basePath, CancellationToken cancellationToken)
    {
        var groups = _processor.SplitByGroup(channels);
        var directory = Path.GetDirectoryName(basePath) ?? Directory.GetCurrentDirectory();
        var baseName = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);

        foreach (var (groupName, groupChannels) in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var safeGroupName = SanitizeFileName(groupName);
            var filePath = Path.Combine(directory, $"{baseName}_{safeGroupName}{extension}");
            await _fileIO.WriteChannelsAsync(filePath, groupChannels, cancellationToken);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var result = new StringBuilder(name.Length);
        foreach (var c in name)
            result.Append(invalidChars.Contains(c) || c == ' ' ? '_' : c);
        return result.ToString();
    }
}
