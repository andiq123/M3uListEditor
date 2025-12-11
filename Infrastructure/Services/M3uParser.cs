using System.Buffers;
using System.Text.RegularExpressions;
using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Services;

/// <summary>
/// High-performance parser for M3U/M3U8 playlist files.
/// </summary>
public sealed partial class M3uParser : IChannelParser
{
    // Regex for extracting tvg-name attribute
    [GeneratedRegex(@"tvg-name\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TvgNameRegex();

    // Regex for extracting group-title attribute
    [GeneratedRegex(@"group-title\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GroupTitleRegex();

    // Regex for extracting the display name after the comma in EXTINF
    [GeneratedRegex(@"#EXTINF\s*:\s*-?\d+[^,]*,\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DisplayNameRegex();

    // Valid stream protocols
    private static readonly HashSet<string> ValidProtocols = new(StringComparer.OrdinalIgnoreCase)
    {
        "http://", "https://", "rtmp://", "rtsp://", "mms://", "mmsh://", "rtp://"
    };

    // File extensions that indicate non-stream resources (skip these)
    private static readonly HashSet<string> InvalidExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".svg", ".webp",
        ".html", ".htm", ".php", ".asp", ".aspx", ".jsp",
        ".css", ".js", ".json", ".xml",
        ".txt", ".pdf", ".doc", ".docx",
        ".zip", ".rar", ".7z", ".tar", ".gz"
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<Channel>> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"M3U file not found: {filePath}", filePath);
        }

        // Read file efficiently with buffering
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        return ParseLines(lines);
    }

    /// <summary>
    /// Parses M3U file lines into channels with optimized processing.
    /// </summary>
    private List<Channel> ParseLines(string[] lines)
    {
        // Pre-allocate with estimated capacity
        var estimatedChannels = Math.Max(100, lines.Length / 3);
        var channels = new List<Channel>(estimatedChannels);
        var id = 0;

        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];

            // Skip empty lines and non-EXTINF comments quickly
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            var trimmedLine = line.AsSpan().Trim();

            // Fast check for EXTINF
            if (!trimmedLine.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            var extinfLine = line.Trim();
            var groupName = string.Empty;
            string? link = null;

            // Extract metadata from EXTINF line
            var (displayName, groupFromExtinf) = ParseExtinfLine(extinfLine);
            if (!string.IsNullOrEmpty(groupFromExtinf))
            {
                groupName = groupFromExtinf;
            }

            // Scan next few lines for group and link
            var searchLimit = Math.Min(i + 5, lines.Length);
            for (var j = i + 1; j < searchLimit; j++)
            {
                var nextLine = lines[j];

                if (string.IsNullOrWhiteSpace(nextLine))
                    continue;

                var nextTrimmed = nextLine.AsSpan().Trim();

                // Check for EXTGRP (group override)
                if (nextTrimmed.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase))
                {
                    groupName = nextLine.Trim()[8..].Trim();
                    continue;
                }

                // Skip other directives
                if (nextTrimmed.StartsWith('#'))
                    continue;

                // This should be the URL line
                var potentialLink = nextLine.Trim();
                if (IsValidStreamUrl(potentialLink))
                {
                    link = NormalizeUrl(potentialLink);
                    i = j; // Move index past the link line
                    break;
                }
            }

            // Only add if we found a valid link
            if (!string.IsNullOrEmpty(link))
            {
                channels.Add(new Channel(id++, displayName, link, groupName));
            }

            i++;
        }

        // Trim excess capacity
        channels.TrimExcess();
        return channels;
    }

    /// <summary>
    /// Parses the EXTINF line to extract display name and group.
    /// </summary>
    private static (string DisplayName, string Group) ParseExtinfLine(string line)
    {
        var displayName = line;
        var group = string.Empty;

        // Try to extract tvg-name first (more reliable)
        var tvgMatch = TvgNameRegex().Match(line);
        if (tvgMatch.Success && !string.IsNullOrWhiteSpace(tvgMatch.Groups[1].Value))
        {
            displayName = tvgMatch.Groups[1].Value.Trim();
        }
        else
        {
            // Fall back to display name after comma
            var nameMatch = DisplayNameRegex().Match(line);
            if (nameMatch.Success && !string.IsNullOrWhiteSpace(nameMatch.Groups[1].Value))
            {
                displayName = nameMatch.Groups[1].Value.Trim();
            }
        }

        // Extract group-title
        var groupMatch = GroupTitleRegex().Match(line);
        if (groupMatch.Success)
        {
            group = groupMatch.Groups[1].Value.Trim();
        }

        return (displayName, group);
    }

    /// <summary>
    /// Validates if a URL is a potentially valid stream URL.
    /// </summary>
    private static bool IsValidStreamUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        // Check for valid protocol
        var hasValidProtocol = false;
        foreach (var protocol in ValidProtocols)
        {
            if (url.StartsWith(protocol, StringComparison.OrdinalIgnoreCase))
            {
                hasValidProtocol = true;
                break;
            }
        }

        if (!hasValidProtocol)
            return false;

        // Basic URL validation
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Check for invalid file extensions
        var path = uri.AbsolutePath;
        foreach (var ext in InvalidExtensions)
        {
            if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Check for obviously invalid URLs
        if (uri.Host.Length < 3)
            return false;

        // Reject localhost/private IPs in most cases (usually test/invalid entries)
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("127.0.0.1", StringComparison.Ordinal) ||
            uri.Host.Equals("0.0.0.0", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Normalizes the URL by cleaning up common issues.
    /// </summary>
    private static string NormalizeUrl(string url)
    {
        // Trim whitespace and quotes
        var normalized = url.Trim().Trim('"', '\'');

        // Remove trailing whitespace or newline characters that might have snuck in
        normalized = normalized.TrimEnd();

        // Handle double slashes in path (except after protocol)
        var protocolEnd = normalized.IndexOf("://", StringComparison.Ordinal);
        if (protocolEnd > 0)
        {
            var beforeProtocol = normalized[..(protocolEnd + 3)];
            var afterProtocol = normalized[(protocolEnd + 3)..];

            // Don't modify the path structure, just clean obvious issues
            // Some streams have intentional double slashes
        }

        return normalized;
    }
}
