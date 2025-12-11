using System.Text.RegularExpressions;
using StreamForge.Core;

namespace StreamForge.Services;

/// <summary>
/// Result from duplicate removal.
/// </summary>
public readonly record struct DedupeResult(IReadOnlyList<Channel> Channels, int RemovedCount);

/// <summary>
/// Processes channels: filtering, sorting, deduplication, merging, splitting.
/// </summary>
public sealed partial class Processor
{
    [GeneratedRegex(@"\s*(hd|sd|fhd|uhd|4k|1080p|720p|480p|360p)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex QualitySuffixRegex();

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex SpecialCharsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public IReadOnlyList<Channel> Filter(IReadOnlyList<Channel> channels, FilterOptions options)
    {
        if (channels.Count == 0) return [];

        IEnumerable<Channel> result = channels;

        if (!string.IsNullOrWhiteSpace(options.GroupFilter))
            result = result.Where(c => c.GroupName.Contains(options.GroupFilter.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(options.NamePattern))
        {
            var pattern = options.NamePattern.Trim();
            if (options.UseRegex)
            {
                try
                {
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    result = result.Where(c => regex.IsMatch(c.Name) || (c.TvgName != null && regex.IsMatch(c.TvgName)));
                }
                catch (RegexParseException)
                {
                    result = result.Where(c => c.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                result = result.Where(c => c.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    (c.TvgName?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false));
            }
        }

        return result.ToList();
    }

    public IReadOnlyList<Channel> Sort(IReadOnlyList<Channel> channels, FilterOptions options)
    {
        if (channels.Count == 0 || options.SortBy == SortOrder.None) return channels;

        IEnumerable<Channel> sorted = options.SortBy switch
        {
            SortOrder.Name => options.SortDescending
                ? channels.OrderByDescending(c => c.TvgName ?? c.Name, StringComparer.OrdinalIgnoreCase)
                : channels.OrderBy(c => c.TvgName ?? c.Name, StringComparer.OrdinalIgnoreCase),

            SortOrder.Group => options.SortDescending
                ? channels.OrderByDescending(c => c.GroupName, StringComparer.OrdinalIgnoreCase)
                : channels.OrderBy(c => c.GroupName, StringComparer.OrdinalIgnoreCase),

            SortOrder.GroupThenName => options.SortDescending
                ? channels.OrderByDescending(c => c.GroupName, StringComparer.OrdinalIgnoreCase)
                          .ThenByDescending(c => c.TvgName ?? c.Name, StringComparer.OrdinalIgnoreCase)
                : channels.OrderBy(c => c.GroupName, StringComparer.OrdinalIgnoreCase)
                          .ThenBy(c => c.TvgName ?? c.Name, StringComparer.OrdinalIgnoreCase),

            _ => channels
        };

        var result = sorted.ToList();
        for (var i = 0; i < result.Count; i++)
            result[i] = result[i] with { Id = i };

        return result;
    }

    public DedupeResult RemoveDuplicates(IReadOnlyList<Channel> channels)
    {
        if (channels.Count == 0) return new DedupeResult([], 0);

        var seenLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueChannels = new List<Channel>(channels.Count);

        foreach (var channel in channels)
        {
            var normalizedLink = NormalizeUrl(channel.Link);
            var normalizedName = NormalizeName(channel.Name);

            if (!seenLinks.Add(normalizedLink))
                continue;

            if (!string.IsNullOrWhiteSpace(normalizedName) && normalizedName.Length > 3 && !IsGenericName(normalizedName))
            {
                if (!seenNames.Add(normalizedName))
                {
                    seenLinks.Remove(normalizedLink);
                    continue;
                }
            }

            uniqueChannels.Add(channel);
        }

        uniqueChannels.TrimExcess();
        return new DedupeResult(uniqueChannels, channels.Count - uniqueChannels.Count);
    }

    public IReadOnlyList<Channel> Merge(params IReadOnlyList<Channel>[] channelLists)
    {
        if (channelLists.Length == 0) return [];

        var seenLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<Channel>();
        var id = 0;

        foreach (var list in channelLists)
            foreach (var channel in list)
            {
                var normalizedLink = NormalizeUrl(channel.Link);
                if (seenLinks.Add(normalizedLink))
                    merged.Add(channel with { Id = id++ });
            }

        return merged;
    }

    public Dictionary<string, IReadOnlyList<Channel>> SplitByGroup(IReadOnlyList<Channel> channels)
    {
        var groups = new Dictionary<string, List<Channel>>(StringComparer.OrdinalIgnoreCase);

        foreach (var channel in channels)
        {
            var groupName = string.IsNullOrWhiteSpace(channel.GroupName) ? "Uncategorized" : channel.GroupName;
            if (!groups.TryGetValue(groupName, out var list))
            {
                list = [];
                groups[groupName] = list;
            }
            list.Add(channel);
        }

        var result = new Dictionary<string, IReadOnlyList<Channel>>(StringComparer.OrdinalIgnoreCase);
        var id = 0;

        foreach (var (group, list) in groups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var reindexed = list.Select(c => c with { Id = id++ }).ToList();
            result[group] = reindexed;
        }

        return result;
    }

    public IReadOnlyList<string> GetGroups(IReadOnlyList<Channel> channels)
    {
        return channels
            .Select(c => string.IsNullOrWhiteSpace(c.GroupName) ? "Uncategorized" : c.GroupName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        var normalized = url.Trim().ToLowerInvariant().TrimEnd('/');
        var queryIndex = normalized.IndexOf('?');
        if (queryIndex > 0)
        {
            var basePath = normalized[..queryIndex];
            var query = normalized[(queryIndex + 1)..];
            var filteredParams = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !IsTrackingParameter(p.ToLowerInvariant()))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            normalized = filteredParams.Count > 0 ? basePath + "?" + string.Join("&", filteredParams) : basePath;
        }

        return normalized.Replace(":80/", "/").Replace(":443/", "/").Replace("://www.", "://");
    }

    private static bool IsTrackingParameter(string param) =>
        param.StartsWith("utm_") || param.StartsWith("session") ||
        param.StartsWith("sid=") || param.StartsWith("token=") ||
        param.StartsWith("t=") || param.StartsWith("ts=") ||
        param.StartsWith("timestamp=") || param.StartsWith("_=") ||
        param.StartsWith("random=") || param.StartsWith("r=") ||
        param.StartsWith("cache=") || param.StartsWith("nocache=");

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var normalized = name;
        if (normalized.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = normalized.IndexOf(',');
            if (commaIndex > 0 && commaIndex < normalized.Length - 1)
                normalized = normalized[(commaIndex + 1)..];
        }

        normalized = QualitySuffixRegex().Replace(normalized.Trim().ToLowerInvariant(), "");
        normalized = SpecialCharsRegex().Replace(normalized, " ");
        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();

        return normalized;
    }

    private static bool IsGenericName(string name) => name is
        "channel" or "test" or "live" or "stream" or "tv" or
        "video" or "audio" or "radio" or "news" or "sports" or
        "movie" or "music" or "entertainment";
}
