using System.Text.RegularExpressions;
using StreamForge.Core;

namespace StreamForge.Services;

/// <summary>
/// High-performance parser for M3U/M3U8 playlist files.
/// </summary>
public sealed partial class Parser
{
    [GeneratedRegex(@"tvg-id\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex TvgIdRegex();

    [GeneratedRegex(@"tvg-name\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex TvgNameRegex();

    [GeneratedRegex(@"tvg-logo\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex TvgLogoRegex();

    [GeneratedRegex(@"group-title\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex GroupTitleRegex();

    [GeneratedRegex(@"#EXTINF\s*:\s*-?\d+[^,]*,\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex DisplayNameRegex();

    [GeneratedRegex(@"x-tvg-url\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex EpgUrlRegex();

    [GeneratedRegex(@"url-tvg\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex EpgUrlAltRegex();

    [GeneratedRegex(@"(\w+[-\w]*)\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex AllAttributesRegex();

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

    private string? _globalEpgUrl;

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

        _globalEpgUrl = null;

        if (lines.Length > 0 && lines[0].StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
        {
            _globalEpgUrl = ExtractMatch(EpgUrlRegex(), lines[0]) ?? ExtractMatch(EpgUrlAltRegex(), lines[0]);
            i = 1;
        }

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
            var channel = ParseExtinfLine(extinfLine, id);
            string? link = null;

            var searchLimit = Math.Min(i + 5, lines.Length);
            for (var j = i + 1; j < searchLimit; j++)
            {
                var nextLine = lines[j];

                if (string.IsNullOrWhiteSpace(nextLine))
                    continue;

                var nextTrimmed = nextLine.AsSpan().Trim();

                if (nextTrimmed.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase))
                {
                    channel = channel with { GroupName = nextLine.Trim()[8..].Trim() };
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
            {
                channel = channel with
                {
                    Id = id++,
                    Link = link,
                    EpgUrl = channel.EpgUrl ?? _globalEpgUrl
                };
                channels.Add(channel);
            }

            i++;
        }

        channels.TrimExcess();
        return channels;
    }

    private static Channel ParseExtinfLine(string line, int id)
    {
        var displayName = line;
        var tvgId = ExtractMatch(TvgIdRegex(), line);
        var tvgName = ExtractMatch(TvgNameRegex(), line);
        var tvgLogo = ExtractMatch(TvgLogoRegex(), line);
        var groupTitle = ExtractMatch(GroupTitleRegex(), line);
        var epgUrl = ExtractMatch(EpgUrlRegex(), line);

        var nameMatch = DisplayNameRegex().Match(line);
        if (nameMatch.Success && !string.IsNullOrWhiteSpace(nameMatch.Groups[1].Value))
            displayName = nameMatch.Groups[1].Value.Trim();
        else if (!string.IsNullOrWhiteSpace(tvgName))
            displayName = tvgName;

        var extraAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var knownAttrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tvg-id", "tvg-name", "tvg-logo", "group-title", "x-tvg-url", "url-tvg"
        };

        foreach (Match match in AllAttributesRegex().Matches(line))
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            if (!knownAttrs.Contains(key) && !string.IsNullOrWhiteSpace(value))
                extraAttributes[key] = value;
        }

        return new Channel
        {
            Id = id,
            Name = displayName,
            TvgId = tvgId,
            TvgName = tvgName,
            TvgLogo = tvgLogo,
            GroupName = groupTitle ?? string.Empty,
            EpgUrl = epgUrl,
            ExtraAttributes = extraAttributes
        };
    }

    private static string? ExtractMatch(Regex regex, string input)
    {
        var match = regex.Match(input);
        return match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value)
            ? match.Groups[1].Value.Trim()
            : null;
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
