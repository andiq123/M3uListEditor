using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using StreamForge.Core;

namespace StreamForge.Services;

/// <summary>
/// Result of testing a stream URL.
/// </summary>
public readonly record struct StreamTestResult(bool IsAlive, StreamInfo? StreamInfo = null);

/// <summary>
/// Validates streams by testing connectivity and filtering dead links.
/// </summary>
public sealed partial class Validator
{
    private readonly HttpClient _client;
    private readonly int _maxConcurrency;

    private static readonly HashSet<string> ValidStreamContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp2t", "video/mp4", "video/mpeg", "video/x-mpegurl", "video/x-ms-asf",
        "video/x-msvideo", "video/x-flv", "video/webm", "video/3gpp", "video/quicktime",
        "audio/mpeg", "audio/aac", "audio/mp4", "audio/x-mpegurl", "audio/x-scpls",
        "application/vnd.apple.mpegurl", "application/x-mpegurl", "application/dash+xml",
        "application/octet-stream", "binary/octet-stream"
    };

    [GeneratedRegex(@"RESOLUTION=(\d+)x(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ResolutionRegex();

    [GeneratedRegex(@"BANDWIDTH=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex BandwidthRegex();

    [GeneratedRegex(@"CODECS=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CodecsRegex();

    public Validator(HttpClient client, int maxConcurrency = 10)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _maxConcurrency = Math.Clamp(maxConcurrency, 1, 50);
    }

    /// <summary>
    /// Filters channels to only those with working streams.
    /// </summary>
    public async Task<IReadOnlyList<Channel>> FilterWorkingAsync(
        IReadOnlyList<Channel> channels,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (channels.Count == 0)
            return [];

        var workingChannels = new ConcurrentBag<(int Index, Channel Channel)>();
        int processed = 0, working = 0, notWorking = 0;

        ReportProgress(progress, channels.Count, 0, 0, 0);

        using var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

        var tasks = channels.Select(async (channel, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await TestWithRetryAsync(channel.Link, cancellationToken);

                if (result.IsAlive)
                {
                    var updatedChannel = result.StreamInfo != null
                        ? channel with { StreamInfo = result.StreamInfo }
                        : channel;
                    workingChannels.Add((index, updatedChannel));
                    Interlocked.Increment(ref working);
                }
                else
                {
                    Interlocked.Increment(ref notWorking);
                }

                var current = Interlocked.Increment(ref processed);
                var reportInterval = channels.Count switch { < 20 => 1, < 100 => 2, < 500 => 5, < 1000 => 10, _ => Math.Max(1, channels.Count / 100) };

                if (current % reportInterval == 0 || current == channels.Count)
                {
                    var percentage = (int)((double)current / channels.Count * 100);
                    ReportProgress(progress, channels.Count, working, notWorking, percentage);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }

        ReportProgress(progress, channels.Count, working, notWorking, 100);

        return workingChannels.OrderBy(x => x.Index).Select(x => x.Channel).ToList();
    }

    private async Task<StreamTestResult> TestWithRetryAsync(string link, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= 2; attempt++)
        {
            try
            {
                var result = await TestLinkAsync(link, cancellationToken);
                if (result.IsAlive) return result;
                if (attempt < 2)
                    await Task.Delay(100 * (attempt + 1), cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch { if (attempt == 2) return new StreamTestResult(false); }
        }
        return new StreamTestResult(false);
    }

    public async Task<StreamTestResult> TestLinkAsync(string link, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(link))
            return new StreamTestResult(false);

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return new StreamTestResult(false);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", "VLC/3.0.18 LibVLC/3.0.18");
            request.Headers.TryAddWithoutValidation("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Icy-MetaData", "1");

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!IsAcceptableStatusCode(response.StatusCode))
                return new StreamTestResult(false);

            var streamInfo = ExtractStreamInfoFromHeaders(response);
            var (isAlive, contentStreamInfo) = await ValidateAndExtractStreamInfoAsync(response, cancellationToken);

            if (!isAlive)
                return new StreamTestResult(false);

            streamInfo = MergeStreamInfo(streamInfo, contentStreamInfo);
            return new StreamTestResult(true, streamInfo);
        }
        catch
        {
            return new StreamTestResult(false);
        }
    }

    private static StreamInfo? ExtractStreamInfoFromHeaders(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("icy-br", out var icyBr))
        {
            var brValue = icyBr.FirstOrDefault();
            if (int.TryParse(brValue, out var br))
                return new StreamInfo { Bitrate = br * 1000 };
        }
        return null;
    }

    private async Task<(bool IsAlive, StreamInfo? Info)> ValidateAndExtractStreamInfoAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

            var buffer = new byte[8192];
            var totalBytesRead = 0;
            var attempts = 0;

            while (attempts < 3 && totalBytesRead < 4096)
            {
                try
                {
                    var bytesRead = await stream.ReadAsync(
                        buffer.AsMemory(totalBytesRead, Math.Min(2048, buffer.Length - totalBytesRead)),
                        timeoutCts.Token);
                    if (bytesRead == 0) break;
                    totalBytesRead += bytesRead;
                    if (totalBytesRead >= 512) break;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    attempts++;
                    if (totalBytesRead > 0) break;
                }
                attempts++;
            }

            if (totalBytesRead == 0)
                return (false, null);

            var content = System.Text.Encoding.ASCII.GetString(buffer, 0, Math.Min(totalBytesRead, 2000));

            if (IsHtmlErrorPage(content))
                return (false, null);

            var streamInfo = DetectStreamInfo(buffer, totalBytesRead, content);
            return (true, streamInfo);
        }
        catch
        {
            return (false, null);
        }
    }

    private static bool IsHtmlErrorPage(string content)
    {
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
               (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && trimmed.Contains("<html", StringComparison.OrdinalIgnoreCase)) ||
               trimmed.StartsWith("404", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("403", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("access denied", StringComparison.OrdinalIgnoreCase);
    }

    private static StreamInfo? DetectStreamInfo(byte[] buffer, int length, string content)
    {
        int? width = null, height = null, bitrate = null;
        string? videoCodec = null, audioCodec = null;

        if (content.TrimStart().StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
        {
            var resMatch = ResolutionRegex().Match(content);
            if (resMatch.Success) { width = int.Parse(resMatch.Groups[1].Value); height = int.Parse(resMatch.Groups[2].Value); }

            var bwMatch = BandwidthRegex().Match(content);
            if (bwMatch.Success) bitrate = int.Parse(bwMatch.Groups[1].Value);

            var codecMatch = CodecsRegex().Match(content);
            if (codecMatch.Success) (videoCodec, audioCodec) = ParseCodecs(codecMatch.Groups[1].Value);
        }

        if (length >= 3)
        {
            if (buffer[0] == 0x47) videoCodec ??= "MPEG-TS";
            if (buffer[0] == 0xFF && (buffer[1] & 0xE0) == 0xE0) audioCodec ??= "MP3";
            if (buffer[0] == 0xFF && (buffer[1] & 0xF0) == 0xF0) audioCodec ??= "AAC";
            if (buffer[0] == 'F' && buffer[1] == 'L' && buffer[2] == 'V') videoCodec ??= "FLV";
            if (buffer[0] == 'I' && buffer[1] == 'D' && buffer[2] == '3') audioCodec ??= "MP3/AAC";
        }

        if (width.HasValue || height.HasValue || bitrate.HasValue || videoCodec != null || audioCodec != null)
            return new StreamInfo { Width = width, Height = height, Bitrate = bitrate, VideoCodec = videoCodec, AudioCodec = audioCodec };

        return null;
    }

    private static (string? Video, string? Audio) ParseCodecs(string codecs)
    {
        string? video = null, audio = null;
        foreach (var codec in codecs.Split(',', StringSplitOptions.TrimEntries))
        {
            var lower = codec.ToLowerInvariant();
            if (lower.StartsWith("avc1")) video = "H.264";
            else if (lower.StartsWith("hvc1") || lower.StartsWith("hev1")) video = "HEVC";
            else if (lower.StartsWith("vp9")) video = "VP9";
            else if (lower.StartsWith("av01")) video = "AV1";
            else if (lower.StartsWith("mp4a")) audio = "AAC";
            else if (lower.StartsWith("ac-3")) audio = "AC3";
            else if (lower.StartsWith("opus")) audio = "Opus";
        }
        return (video, audio);
    }

    private static StreamInfo? MergeStreamInfo(StreamInfo? a, StreamInfo? b)
    {
        if (a == null) return b;
        if (b == null) return a;
        return new StreamInfo
        {
            Width = a.Width ?? b.Width, Height = a.Height ?? b.Height,
            Bitrate = a.Bitrate ?? b.Bitrate, VideoCodec = a.VideoCodec ?? b.VideoCodec, AudioCodec = a.AudioCodec ?? b.AudioCodec
        };
    }

    private static bool IsAcceptableStatusCode(System.Net.HttpStatusCode statusCode) =>
        statusCode == System.Net.HttpStatusCode.OK || statusCode == System.Net.HttpStatusCode.PartialContent ||
        ((int)statusCode >= 200 && (int)statusCode < 300);

    private static void ReportProgress(IProgress<ProgressReport>? progress, int total, int working, int notWorking, int percentage)
    {
        progress?.Report(new ProgressReport
        {
            ChannelsCountTotal = total,
            WorkingChannelsCount = working,
            NotWorkingChannelsCount = notWorking,
            PercentageCompleted = Math.Clamp(percentage, 0, 100),
            CurrentActivity = "Validating streams..."
        });
    }
}
