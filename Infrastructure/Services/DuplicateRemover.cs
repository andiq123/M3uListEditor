using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Services;

/// <summary>
/// Removes duplicate channels using URL normalization and O(n) HashSet-based algorithm.
/// Detects duplicates even with slight URL variations.
/// </summary>
public sealed class DuplicateRemover : IDuplicateRemover
{
    /// <inheritdoc />
    public DuplicateRemovalResult RemoveDuplicates(IReadOnlyList<Channel> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);

        if (channels.Count == 0)
        {
            return new DuplicateRemovalResult([], 0);
        }

        // Use normalized URLs for duplicate detection
        var seenLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueChannels = new List<Channel>(channels.Count);

        foreach (var channel in channels)
        {
            var normalizedLink = NormalizeUrl(channel.Link);
            var normalizedName = NormalizeName(channel.Name);

            // Check for duplicate by normalized URL
            if (!seenLinks.Add(normalizedLink))
            {
                continue; // Duplicate URL
            }

            // Also check for same name with different URL (likely duplicate with different server)
            // Only skip if name is meaningful (not empty or generic)
            if (!string.IsNullOrWhiteSpace(normalizedName) &&
                normalizedName.Length > 3 &&
                !IsGenericName(normalizedName))
            {
                if (!seenNames.Add(normalizedName))
                {
                    // Same name but different URL - keep the first one
                    // Remove from seenLinks since we're not adding this channel
                    seenLinks.Remove(normalizedLink);
                    continue;
                }
            }

            uniqueChannels.Add(channel);
        }

        var removedCount = channels.Count - uniqueChannels.Count;
        uniqueChannels.TrimExcess();

        return new DuplicateRemovalResult(uniqueChannels, removedCount);
    }

    /// <summary>
    /// Normalizes a URL to detect duplicates with slight variations.
    /// </summary>
    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var normalized = url.Trim().ToLowerInvariant();

        // Remove trailing slashes
        normalized = normalized.TrimEnd('/');

        // Remove common tracking/session parameters
        var queryIndex = normalized.IndexOf('?');
        if (queryIndex > 0)
        {
            var basePath = normalized[..queryIndex];
            var query = normalized[(queryIndex + 1)..];

            // Parse and filter query parameters
            var filteredParams = new List<string>();
            foreach (var param in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var paramLower = param.ToLowerInvariant();

                // Skip common tracking/session parameters
                if (IsTrackingParameter(paramLower))
                    continue;

                filteredParams.Add(param);
            }

            if (filteredParams.Count > 0)
            {
                // Sort parameters for consistent comparison
                filteredParams.Sort(StringComparer.OrdinalIgnoreCase);
                normalized = basePath + "?" + string.Join("&", filteredParams);
            }
            else
            {
                normalized = basePath;
            }
        }

        // Normalize protocol
        if (normalized.StartsWith("http://"))
        {
            // Keep as-is, but note that http and https versions might be same stream
        }

        // Remove default ports
        normalized = normalized
            .Replace(":80/", "/")
            .Replace(":443/", "/")
            .Replace(":80?", "?")
            .Replace(":443?", "?");

        // Remove www. prefix for comparison
        normalized = normalized
            .Replace("://www.", "://");

        return normalized;
    }

    /// <summary>
    /// Checks if a query parameter is a tracking/session parameter that should be ignored.
    /// </summary>
    private static bool IsTrackingParameter(string param)
    {
        // Common tracking/session parameters to ignore
        return param.StartsWith("utm_") ||
               param.StartsWith("session") ||
               param.StartsWith("sid=") ||
               param.StartsWith("token=") ||
               param.StartsWith("t=") ||
               param.StartsWith("ts=") ||
               param.StartsWith("timestamp=") ||
               param.StartsWith("_=") ||
               param.StartsWith("random=") ||
               param.StartsWith("r=") ||
               param.StartsWith("cache=") ||
               param.StartsWith("nocache=");
    }

    /// <summary>
    /// Normalizes a channel name for duplicate detection.
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = name;

        // Remove EXTINF prefix if present
        if (normalized.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = normalized.IndexOf(',');
            if (commaIndex > 0 && commaIndex < normalized.Length - 1)
            {
                normalized = normalized[(commaIndex + 1)..];
            }
        }

        // Normalize: lowercase, trim, remove extra spaces
        normalized = normalized.Trim().ToLowerInvariant();

        // Remove common suffixes like "HD", "SD", "FHD", resolution markers
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\s*(hd|sd|fhd|uhd|4k|1080p|720p|480p|360p)\s*$",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove special characters and extra whitespace
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\w\s]", " ");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }

    /// <summary>
    /// Checks if a name is too generic to use for duplicate detection.
    /// </summary>
    private static bool IsGenericName(string normalizedName)
    {
        // Names that are too generic to use for duplicate detection
        return normalizedName is
            "channel" or "test" or "live" or "stream" or "tv" or
            "video" or "audio" or "radio" or "news" or "sports" or
            "movie" or "music" or "entertainment";
    }
}
