using StreamForge.Core;

namespace StreamForge.Cli;

/// <summary>
/// CLI argument models.
/// </summary>
public sealed record ParsedArguments(
    string? SourcePath,
    List<string> SourcePaths,
    string? ExportPath,
    TimeSpan? Timeout,
    bool RemoveDoubles,
    int MaxConcurrency,
    string? GroupFilter,
    string? NamePattern,
    bool UseRegex,
    SortOrder SortBy,
    bool SortDescending,
    bool SplitByGroup,
    bool MergeFiles,
    bool SkipValidation,
    bool AutoCategorize,
    bool DetectLanguage,
    bool DetectContentDuplicates,
    string? RenamePattern,
    string? RenameReplacement);
