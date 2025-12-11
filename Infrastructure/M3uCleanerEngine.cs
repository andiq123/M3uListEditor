using Core.Interfaces;
using Core.Models;

namespace Infrastructure;

/// <summary>
/// Main processing engine for M3U file cleaning.
/// </summary>
public sealed class M3uCleanerEngine
{
    private readonly IChannelParser _parser;
    private readonly IChannelFilter _filter;
    private readonly IDuplicateRemover _duplicateRemover;
    private readonly IFileHandler _fileHandler;

    public M3uCleanerEngine(
        IChannelParser parser,
        IChannelFilter filter,
        IDuplicateRemover duplicateRemover,
        IFileHandler fileHandler)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        _duplicateRemover = duplicateRemover ?? throw new ArgumentNullException(nameof(duplicateRemover));
        _fileHandler = fileHandler ?? throw new ArgumentNullException(nameof(fileHandler));
    }

    /// <summary>
    /// Processes an M3U file and creates a cleaned version with only working channels.
    /// </summary>
    public async Task<FinalChannelReport> ProcessAsync(
        AppSettings settings,
        IProgress<ProgressReportModel>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrEmpty(settings.SourcePath) || string.IsNullOrEmpty(settings.ExportPath))
        {
            throw new ArgumentException("Source and export paths must be specified.", nameof(settings));
        }

        var channels = await _parser.ParseAsync(settings.SourcePath, cancellationToken);

        var doublesRemoved = 0;
        if (settings.RemoveDoubles)
        {
            var result = _duplicateRemover.RemoveDuplicates(channels);
            channels = result.Channels;
            doublesRemoved = result.RemovedCount;
        }

        var workingChannels = await _filter.FilterWorkingAsync(channels, progress, cancellationToken);
        await _fileHandler.WriteChannelsAsync(settings.ExportPath, workingChannels, cancellationToken);

        return new FinalChannelReport(
            WorkingChannelsCount: workingChannels.Count,
            TotalChannelsCount: channels.Count,
            DoublesRemovedCount: doublesRemoved);
    }
}
