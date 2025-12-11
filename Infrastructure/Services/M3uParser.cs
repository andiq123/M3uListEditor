using System.Text.RegularExpressions;
using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Services;

/// <summary>
/// High-performance parser for M3U/M3U8 playlist files.
/// </summary>
public sealed partial class M3uParser : IChannelParser
{
    [GeneratedRegex(@"tvg-name\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TvgNameRegex();

    [GeneratedRegex(@"group-title\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GroupTitleRegex();

    [GeneratedRegex(@"#EXTINF\s*:\s*-?\d+[^,]*,\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DisplayNameRegex();

    private static readonly HashSet<string> ValidProtocols = new(StringComparer.OrdinalIgnoreCase)
    {
        "http://", "https://", "rtmp://", "rtsp://", "mms://", "mmsh://", "rtp://"
    };

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
            throw new FileNotFoundException($"M3U file not found: {filePath}", filePath);

        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        return ParseLines(lines);
    }

    private List<Channel> ParseLines(string[] lines)
    {
        var channels = new List<Channel>(Math.Max(100, lines.Length / 3));
        var id = 0;
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            var trimmedLine = line.AsSpan().Trim();

            if (!trimmedLine.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            var extinfLine = line.Trim();
            var groupName = string.Empty;
            string? link = null;

            var (displayName, groupFromExtinf) = ParseExtinfLine(extinfLine);
            if (!string.IsNullOrEmpty(groupFromExtinf))
                groupName = groupFromExtinf;

            var searchLimit = Math.Min(i + 5, lines.Length);
            for (var j = i + 1; j < searchLimit; j++)
            {
                var nextLine = lines[j];

                if (string.IsNullOrWhiteSpace(nextLine))
                    continue;

                var nextTrimmed = nextLine.AsSpan().Trim();

                if (nextTrimmed.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase))
                {
                    groupName = nextLine.Trim()[8..].Trim();
                    continue;
                }

                if (nextTrimmed.StartsWith('#'))
                    continue;

                var potentialLink = nextLine.Trim();
                if (IsValidStreamUrl(potentialLink))
                {
                    link = potentialLink.Trim().Trim('"', '\'');
                    i = j;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(link))
                channels.Add(new Channel(id++, displayName, link, groupName));

            i++;
        }

        channels.TrimExcess();
        return channels;
    }

    private static (string DisplayName, string Group) ParseExtinfLine(string line)
    {
        var displayName = line;
        var group = string.Empty;

        var tvgMatch = TvgNameRegex().Match(line);
        if (tvgMatch.Success && !string.IsNullOrWhiteSpace(tvgMatch.Groups[1].Value))
        {
            displayName = tvgMatch.Groups[1].Value.Trim();
        }
        else
        {
            var nameMatch = DisplayNameRegex().Match(line);
            if (nameMatch.Success && !string.IsNullOrWhiteSpace(nameMatch.Groups[1].Value))
                displayName = nameMatch.Groups[1].Value.Trim();
        }

        var groupMatch = GroupTitleRegex().Match(line);
        if (groupMatch.Success)
            group = groupMatch.Groups[1].Value.Trim();

        return (displayName, group);
    }

    private static bool IsValidStreamUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

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

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var path = uri.AbsolutePath;
        foreach (var ext in InvalidExtensions)
        {
            if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (uri.Host.Length < 3)
            return false;

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("127.0.0.1", StringComparison.Ordinal) ||
            uri.Host.Equals("0.0.0.0", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
