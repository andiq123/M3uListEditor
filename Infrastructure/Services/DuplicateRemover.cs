using System.Text.RegularExpressions;
using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Services;

/// <summary>
/// Removes duplicate channels using URL normalization and O(n) HashSet-based algorithm.
/// </summary>
public sealed partial class DuplicateRemover : IDuplicateRemover
{
    [GeneratedRegex(@"\s*(hd|sd|fhd|uhd|4k|1080p|720p|480p|360p)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex QualitySuffixRegex();

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex SpecialCharsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    /// <inheritdoc />
    public DuplicateRemovalResult RemoveDuplicates(IReadOnlyList<Channel> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);

        if (channels.Count == 0)
            return new DuplicateRemovalResult([], 0);

        var seenLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueChannels = new List<Channel>(channels.Count);

        foreach (var channel in channels)
        {
            var normalizedLink = NormalizeUrl(channel.Link);
            var normalizedName = NormalizeName(channel.Name);

            if (!seenLinks.Add(normalizedLink))
                continue;

            if (!string.IsNullOrWhiteSpace(normalizedName) &&
                normalizedName.Length > 3 &&
                !IsGenericName(normalizedName))
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
        return new DuplicateRemovalResult(uniqueChannels, channels.Count - uniqueChannels.Count);
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

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

            normalized = filteredParams.Count > 0
                ? basePath + "?" + string.Join("&", filteredParams)
                : basePath;
        }

        return normalized
            .Replace(":80/", "/").Replace(":443/", "/")
            .Replace(":80?", "?").Replace(":443?", "?")
            .Replace("://www.", "://");
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
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

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
