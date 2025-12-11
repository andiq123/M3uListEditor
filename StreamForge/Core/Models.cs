namespace StreamForge.Core;

#region Enums

/// <summary>
/// Channel category types for auto-categorization.
/// </summary>
public enum ChannelCategory
{
    Unknown, News, Sports, Movies, Series, Documentary,
    Kids, Music, Entertainment, Lifestyle, Religious, Education, Adult
}

/// <summary>
/// Sort order for channels.
/// </summary>
public enum SortOrder { None, Name, Group, GroupThenName }

#endregion

#region Records

/// <summary>
/// Detected language for a channel.
/// </summary>
public sealed record LanguageInfo
{
    public string Code { get; init; } = "en";
    public string Name { get; init; } = "English";
    public string? Country { get; init; }
}

/// <summary>
/// Stream quality and codec information.
/// </summary>
public sealed record StreamInfo
{
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? Bitrate { get; init; }
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public string? Resolution => Width.HasValue && Height.HasValue ? $"{Width}x{Height}" : null;
}

/// <summary>
/// Represents a channel in an M3U playlist.
/// </summary>
public sealed record Channel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Link { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public string? TvgId { get; init; }
    public string? TvgName { get; init; }
    public string? TvgLogo { get; init; }
    public string? EpgUrl { get; init; }
    public StreamInfo? StreamInfo { get; init; }
    public Dictionary<string, string> ExtraAttributes { get; init; } = new();
    public ChannelCategory Category { get; init; } = ChannelCategory.Unknown;
    public LanguageInfo? Language { get; init; }
    public string? ContentHash { get; init; }

    public Channel() { }
    public Channel(int id, string name, string link, string groupName = "")
    {
        Id = id; Name = name; Link = link; GroupName = groupName;
    }
}

/// <summary>
/// Options for filtering and sorting channels.
/// </summary>
public sealed record FilterOptions
{
    public string? GroupFilter { get; init; }
    public string? NamePattern { get; init; }
    public bool UseRegex { get; init; }
    public SortOrder SortBy { get; init; } = SortOrder.None;
    public bool SortDescending { get; init; }
}

/// <summary>
/// Application configuration.
/// </summary>
public sealed record AppSettings
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public string? SourcePath { get; init; }
    public List<string> SourcePaths { get; init; } = [];
    public string? ExportPath { get; init; }
    public TimeSpan? Timeout { get; init; }
    public bool RemoveDoubles { get; init; } = true;
    public bool IsLinkSourcePath { get; init; }
    public int MaxConcurrency { get; init; } = 10;
    public string? GroupFilter { get; init; }
    public string? NamePattern { get; init; }
    public bool UseRegex { get; init; }
    public SortOrder SortBy { get; init; } = SortOrder.None;
    public bool SortDescending { get; init; }
    public bool SplitByGroup { get; init; }
    public bool MergeFiles { get; init; }
    public bool SkipValidation { get; init; }
    public bool AutoCategorize { get; init; }
    public bool DetectLanguage { get; init; }
    public bool DetectContentDuplicates { get; init; }
    public string? RenamePattern { get; init; }
    public string? RenameReplacement { get; init; }
    public bool RenameUseRegex { get; init; }

    public TimeSpan EffectiveTimeout => Timeout ?? DefaultTimeout;

    public FilterOptions ToFilterOptions() => new()
    {
        GroupFilter = GroupFilter,
        NamePattern = NamePattern,
        UseRegex = UseRegex,
        SortBy = SortBy,
        SortDescending = SortDescending
    };
}

/// <summary>
/// Final report after processing.
/// </summary>
public sealed record ProcessingReport(
    int WorkingChannelsCount,
    int TotalChannelsCount,
    int DoublesRemovedCount,
    int OriginalCount = 0,
    int GroupCount = 0,
    int CategorizedCount = 0,
    int LanguagesDetectedCount = 0);

/// <summary>
/// Progress during processing.
/// </summary>
public record ProgressReport
{
    public int PercentageCompleted { get; init; }
    public int ChannelsCountTotal { get; init; }
    public int WorkingChannelsCount { get; init; }
    public int NotWorkingChannelsCount { get; init; }
    public string CurrentActivity { get; init; } = "Processing...";
}

#endregion
