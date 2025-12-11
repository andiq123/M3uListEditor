using System.Security.Cryptography;
using System.Text.RegularExpressions;
using StreamForge.Core;

namespace StreamForge.Services;

/// <summary>
/// Enhances channels with smart detection, categorization, and metadata enrichment.
/// </summary>
public sealed partial class Enhancer
{
    private readonly HttpClient _httpClient;

    private static readonly Dictionary<ChannelCategory, string[]> CategoryKeywords = new()
    {
        [ChannelCategory.News] = ["news", "cnn", "bbc", "fox news", "msnbc", "cnbc", "sky news", "al jazeera", "euronews", "france 24", "dw", "rt", "noticias", "nachrichten", "actualités", "akhbar", "haberleri", "wiadomości", "nyheter", "uutiset"],
        [ChannelCategory.Sports] = ["sport", "espn", "fox sports", "sky sports", "bein", "dazn", "eurosport", "nba", "nfl", "mlb", "nhl", "football", "soccer", "tennis", "golf", "racing", "f1", "moto gp", "ufc", "wwe", "boxing", "cricket", "rugby"],
        [ChannelCategory.Movies] = ["movie", "cinema", "film", "hbo", "cinemax", "starz", "showtime", "tcm", "amc", "hallmark", "lifetime", "fx", "tnt", "tbs", "filmy", "kino", "pelicula", "cine"],
        [ChannelCategory.Series] = ["series", "netflix", "amazon", "hulu", "apple tv", "paramount+", "peacock", "discovery+", "drama", "comedy central", "syfy", "usa network"],
        [ChannelCategory.Documentary] = ["documentary", "docu", "discovery", "national geographic", "nat geo", "history", "animal planet", "smithsonian", "pbs", "arte", "bbc four", "curiosity"],
        [ChannelCategory.Kids] = ["kids", "children", "cartoon", "nick", "nickelodeon", "disney", "baby", "junior", "toon", "boomerang", "pbs kids", "cbbc", "cbeebies", "jim jam", "duck tv", "baby tv"],
        [ChannelCategory.Music] = ["music", "mtv", "vh1", "vevo", "trace", "cmtv", "club", "hits", "radio", "concert", "musica", "musik", "musique"],
        [ChannelCategory.Entertainment] = ["entertainment", "e!", "bravo", "tlc", "reality", "talk show", "variety", "game show", "comedy", "late night"],
        [ChannelCategory.Lifestyle] = ["lifestyle", "food", "cooking", "travel", "hgtv", "diy", "home", "garden", "fashion", "health", "wellness"],
        [ChannelCategory.Religious] = ["religious", "god", "church", "christian", "catholic", "gospel", "faith", "prayer", "bible", "jesus", "islamic", "quran", "jewish", "hindu", "buddhist", "ewtn", "tbn", "daystar"],
        [ChannelCategory.Education] = ["education", "learning", "science", "school", "university", "lecture", "ted", "khan academy", "coursera"],
        [ChannelCategory.Adult] = ["adult", "xxx", "18+", "playboy", "hustler", "penthouse", "erotic", "hot"]
    };

    private static readonly Dictionary<string, LanguageInfo> LanguagePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["US"] = new() { Code = "en", Name = "English", Country = "USA" },
        ["USA"] = new() { Code = "en", Name = "English", Country = "USA" },
        ["UK"] = new() { Code = "en", Name = "English", Country = "UK" },
        ["GB"] = new() { Code = "en", Name = "English", Country = "UK" },
        ["CA"] = new() { Code = "en", Name = "English", Country = "Canada" },
        ["AU"] = new() { Code = "en", Name = "English", Country = "Australia" },
        ["DE"] = new() { Code = "de", Name = "German", Country = "Germany" },
        ["AT"] = new() { Code = "de", Name = "German", Country = "Austria" },
        ["FR"] = new() { Code = "fr", Name = "French", Country = "France" },
        ["ES"] = new() { Code = "es", Name = "Spanish", Country = "Spain" },
        ["MX"] = new() { Code = "es", Name = "Spanish", Country = "Mexico" },
        ["IT"] = new() { Code = "it", Name = "Italian", Country = "Italy" },
        ["PT"] = new() { Code = "pt", Name = "Portuguese", Country = "Portugal" },
        ["BR"] = new() { Code = "pt", Name = "Portuguese", Country = "Brazil" },
        ["NL"] = new() { Code = "nl", Name = "Dutch", Country = "Netherlands" },
        ["PL"] = new() { Code = "pl", Name = "Polish", Country = "Poland" },
        ["RU"] = new() { Code = "ru", Name = "Russian", Country = "Russia" },
        ["TR"] = new() { Code = "tr", Name = "Turkish", Country = "Turkey" },
        ["AR"] = new() { Code = "ar", Name = "Arabic", Country = "Arab" },
        ["IN"] = new() { Code = "hi", Name = "Hindi", Country = "India" },
        ["JP"] = new() { Code = "ja", Name = "Japanese", Country = "Japan" },
        ["KR"] = new() { Code = "ko", Name = "Korean", Country = "South Korea" },
        ["CN"] = new() { Code = "zh", Name = "Chinese", Country = "China" },
        ["RO"] = new() { Code = "ro", Name = "Romanian", Country = "Romania" },
        ["GR"] = new() { Code = "el", Name = "Greek", Country = "Greece" },
        ["SE"] = new() { Code = "sv", Name = "Swedish", Country = "Sweden" },
        ["NO"] = new() { Code = "no", Name = "Norwegian", Country = "Norway" },
        ["DK"] = new() { Code = "da", Name = "Danish", Country = "Denmark" },
        ["FI"] = new() { Code = "fi", Name = "Finnish", Country = "Finland" },
        ["ENGLISH"] = new() { Code = "en", Name = "English" },
        ["GERMAN"] = new() { Code = "de", Name = "German" },
        ["FRENCH"] = new() { Code = "fr", Name = "French" },
        ["SPANISH"] = new() { Code = "es", Name = "Spanish" },
        ["ITALIAN"] = new() { Code = "it", Name = "Italian" },
        ["PORTUGUESE"] = new() { Code = "pt", Name = "Portuguese" },
        ["DUTCH"] = new() { Code = "nl", Name = "Dutch" },
        ["POLISH"] = new() { Code = "pl", Name = "Polish" },
        ["RUSSIAN"] = new() { Code = "ru", Name = "Russian" },
        ["TURKISH"] = new() { Code = "tr", Name = "Turkish" },
        ["ARABIC"] = new() { Code = "ar", Name = "Arabic" },
        ["HINDI"] = new() { Code = "hi", Name = "Hindi" },
        ["LATINO"] = new() { Code = "es", Name = "Spanish", Country = "Latin America" },
        ["PERSIAN"] = new() { Code = "fa", Name = "Persian" },
        ["FARSI"] = new() { Code = "fa", Name = "Persian" },
        ["GREEK"] = new() { Code = "el", Name = "Greek" },
        ["ROMANIAN"] = new() { Code = "ro", Name = "Romanian" },
        ["HUNGARIAN"] = new() { Code = "hu", Name = "Hungarian" },
        ["CZECH"] = new() { Code = "cs", Name = "Czech" },
        ["SLOVAK"] = new() { Code = "sk", Name = "Slovak" },
        ["SERBIAN"] = new() { Code = "sr", Name = "Serbian" },
        ["CROATIAN"] = new() { Code = "hr", Name = "Croatian" },
        ["BULGARIAN"] = new() { Code = "bg", Name = "Bulgarian" },
        ["UKRAINIAN"] = new() { Code = "uk", Name = "Ukrainian" },
    };

    [GeneratedRegex(@"^([A-Z]{2})[\s:\-\|]+", RegexOptions.IgnoreCase)]
    private static partial Regex CountryPrefixRegex();

    [GeneratedRegex(@"\[([A-Z]{2})\]|\(([A-Z]{2})\)", RegexOptions.IgnoreCase)]
    private static partial Regex CountryBracketRegex();

    public Enhancer(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public IReadOnlyList<Channel> AutoCategorize(IReadOnlyList<Channel> channels)
    {
        var result = new List<Channel>(channels.Count);
        foreach (var channel in channels)
        {
            var category = DetectCategory(channel);
            var updatedChannel = channel with { Category = category };
            if (category != ChannelCategory.Unknown && (string.IsNullOrEmpty(channel.GroupName) || channel.GroupName.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)))
                updatedChannel = updatedChannel with { GroupName = category.ToString() };
            result.Add(updatedChannel);
        }
        return result;
    }

    public IReadOnlyList<Channel> DetectLanguage(IReadOnlyList<Channel> channels)
    {
        var result = new List<Channel>(channels.Count);
        foreach (var channel in channels)
            result.Add(channel with { Language = DetectChannelLanguage(channel) });
        return result;
    }

    public async Task<IReadOnlyList<Channel>> DetectDuplicateContentAsync(
        IReadOnlyList<Channel> channels,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<Channel>(channels.Count);
        var seenHashes = new Dictionary<string, int>();
        var processed = 0;

        foreach (var channel in channels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hash = await ComputeStreamHashAsync(channel.Link, cancellationToken);
            var updatedChannel = channel with { ContentHash = hash };

            if (!string.IsNullOrEmpty(hash))
            {
                if (seenHashes.TryGetValue(hash, out var existingIndex))
                {
                    var extras = new Dictionary<string, string>(updatedChannel.ExtraAttributes) { ["duplicate-of"] = result[existingIndex].Name };
                    updatedChannel = updatedChannel with { ExtraAttributes = extras };
                }
                else
                {
                    seenHashes[hash] = result.Count;
                }
            }

            result.Add(updatedChannel);
            processed++;

            if (processed % 5 == 0 || processed == channels.Count)
            {
                progress?.Report(new ProgressReport
                {
                    ChannelsCountTotal = channels.Count,
                    PercentageCompleted = (int)((double)processed / channels.Count * 100),
                    CurrentActivity = "Enhancing: Scanning content signatures..."
                });
            }
        }

        return result;
    }

    public IReadOnlyList<Channel> RenameChannels(IReadOnlyList<Channel> channels, string pattern, string replacement, bool useRegex = false)
    {
        var result = new List<Channel>(channels.Count);
        Regex? regex = null;
        if (useRegex)
        {
            try { regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled); }
            catch (RegexParseException) { return channels; }
        }

        foreach (var channel in channels)
        {
            var newName = channel.TvgName ?? channel.Name;
            var newTvgName = channel.TvgName;

            if (useRegex && regex != null)
            {
                newName = regex.Replace(newName, replacement);
                if (newTvgName != null) newTvgName = regex.Replace(newTvgName, replacement);
            }
            else
            {
                newName = newName.Replace(pattern, replacement, StringComparison.OrdinalIgnoreCase);
                if (newTvgName != null) newTvgName = newTvgName.Replace(pattern, replacement, StringComparison.OrdinalIgnoreCase);
            }

            result.Add(channel with { Name = newName.Trim(), TvgName = newTvgName?.Trim() });
        }

        return result;
    }

    private static ChannelCategory DetectCategory(Channel channel)
    {
        var searchText = $"{channel.Name} {channel.TvgName} {channel.GroupName}".ToLowerInvariant();
        var groupLower = channel.GroupName.ToLowerInvariant();

        foreach (var (category, keywords) in CategoryKeywords)
            foreach (var keyword in keywords)
                if (groupLower.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return category;

        foreach (var (category, keywords) in CategoryKeywords)
            foreach (var keyword in keywords)
                if (searchText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return category;

        return ChannelCategory.Unknown;
    }

    private static LanguageInfo? DetectChannelLanguage(Channel channel)
    {
        var name = channel.TvgName ?? channel.Name;
        var group = channel.GroupName;

        var prefixMatch = CountryPrefixRegex().Match(name);
        if (prefixMatch.Success)
        {
            var code = prefixMatch.Groups[1].Value.ToUpperInvariant();
            if (LanguagePatterns.TryGetValue(code, out var lang)) return lang;
        }

        var bracketMatch = CountryBracketRegex().Match(name);
        if (bracketMatch.Success)
        {
            var code = (bracketMatch.Groups[1].Value + bracketMatch.Groups[2].Value).ToUpperInvariant();
            if (LanguagePatterns.TryGetValue(code, out var lang)) return lang;
        }

        foreach (var (key, lang) in LanguagePatterns)
        {
            if (key.Length <= 2) continue;
            if (group.Contains(key, StringComparison.OrdinalIgnoreCase) || name.Contains(key, StringComparison.OrdinalIgnoreCase))
                return lang;
        }

        return null;
    }

    private async Task<string?> ComputeStreamHashAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "VLC/3.0.18");
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 8191);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[8192];
            var totalRead = 0;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            while (totalRead < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cts.Token);
                if (read == 0) break;
                totalRead += read;
                if (totalRead >= 2048) break;
            }

            if (totalRead < 256) return null;

            var hash = SHA256.HashData(buffer.AsSpan(0, totalRead));
            return Convert.ToHexString(hash)[..16];
        }
        catch { return null; }
    }
}
