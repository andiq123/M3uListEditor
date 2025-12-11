using Core.Interfaces;

namespace Infrastructure.Services;

/// <summary>
/// Tests if streaming links are alive by validating response headers and content.
/// </summary>
public sealed class SignalTester : ISignalTester
{
    private readonly HttpClient _client;

    // Valid content types for streaming media
    private static readonly HashSet<string> ValidStreamContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Video types
        "video/mp2t",
        "video/mp4",
        "video/mpeg",
        "video/x-mpegurl",
        "video/x-ms-asf",
        "video/x-msvideo",
        "video/x-flv",
        "video/webm",
        "video/3gpp",
        "video/quicktime",

        // Audio types
        "audio/mpeg",
        "audio/aac",
        "audio/mp4",
        "audio/x-mpegurl",
        "audio/x-scpls",

        // Application types (HLS, DASH)
        "application/vnd.apple.mpegurl",
        "application/x-mpegurl",
        "application/dash+xml",
        "application/octet-stream",

        // Generic binary stream
        "binary/octet-stream"
    };

    public SignalTester(HttpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public async Task<bool> IsLinkAliveAsync(string link, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(link))
            return false;

        // Validate URI format before making request
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        try
        {
            // Go straight to GET request - more reliable for streams
            // HEAD often doesn't work with streaming servers
            return await TryGetRequestAsync(uri, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> TryGetRequestAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            AddStreamingHeaders(request);

            // Only read headers first, don't download the entire stream
            using var response = await _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            // Accept more status codes - some streams return redirects or partial content
            if (!IsAcceptableStatusCode(response.StatusCode))
                return false;

            // Check content type - if valid streaming type, try to read data
            var hasValidContentType = IsValidStreamingContentType(response);

            // Always try to read data to verify stream is actually working
            return await CanReadStreamDataAsync(response, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private static void AddStreamingHeaders(HttpRequestMessage request)
    {
        // Add headers that streaming servers expect
        request.Headers.TryAddWithoutValidation("User-Agent", "VLC/3.0.18 LibVLC/3.0.18");
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
        request.Headers.TryAddWithoutValidation("Icy-MetaData", "1");
    }

    private static bool IsAcceptableStatusCode(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.OK => true,
            System.Net.HttpStatusCode.PartialContent => true,
            System.Net.HttpStatusCode.NoContent => false, // No content = not a stream
            _ => (int)statusCode >= 200 && (int)statusCode < 300
        };
    }

    private static bool IsValidStreamingContentType(HttpResponseMessage response)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (string.IsNullOrEmpty(contentType))
            return false;

        return ValidStreamContentTypes.Contains(contentType);
    }

    private static async Task<bool> CanReadStreamDataAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            // Use a reasonable timeout for slow streams (use the HttpClient's timeout as reference)
            // Give streams up to 8 seconds to start sending data
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

            // Try to read data in chunks, some streams are slow to start
            var buffer = new byte[4096];
            var totalBytesRead = 0;
            var attempts = 0;
            const int maxAttempts = 3;

            while (attempts < maxAttempts && totalBytesRead < 512)
            {
                try
                {
                    var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalBytesRead, Math.Min(1024, buffer.Length - totalBytesRead)), timeoutCts.Token);

                    if (bytesRead == 0)
                    {
                        // Stream ended - if we got some data, check if it's valid
                        break;
                    }

                    totalBytesRead += bytesRead;

                    // If we got enough data, we can check it
                    if (totalBytesRead >= 64)
                        break;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout on this attempt, but main cancellation not requested
                    attempts++;
                    if (totalBytesRead > 0)
                        break; // We got some data, check it
                }

                attempts++;
            }

            if (totalBytesRead == 0)
                return false;

            // Check for common error page signatures (HTML error pages)
            if (totalBytesRead >= 10)
            {
                // Check first bytes for HTML markers
                var header = System.Text.Encoding.ASCII.GetString(buffer, 0, Math.Min(totalBytesRead, 200));
                var trimmedHeader = header.TrimStart();

                // If it looks like HTML, it's probably an error page, not a stream
                if (trimmedHeader.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                    trimmedHeader.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                    trimmedHeader.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && trimmedHeader.Contains("<html", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Check for common error messages in plain text
                if (trimmedHeader.StartsWith("404", StringComparison.OrdinalIgnoreCase) ||
                    trimmedHeader.StartsWith("403", StringComparison.OrdinalIgnoreCase) ||
                    trimmedHeader.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                    trimmedHeader.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                    trimmedHeader.Contains("access denied", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Check for valid stream signatures
            if (totalBytesRead >= 3)
            {
                // Check for MPEG-TS sync byte (0x47)
                if (buffer[0] == 0x47)
                    return true;

                // Check for ID3 tag (MP3/AAC streams)
                if (buffer[0] == 'I' && buffer[1] == 'D' && buffer[2] == '3')
                    return true;

                // Check for MP3 frame sync
                if (buffer[0] == 0xFF && (buffer[1] & 0xE0) == 0xE0)
                    return true;

                // Check for AAC ADTS sync
                if (buffer[0] == 0xFF && (buffer[1] & 0xF0) == 0xF0)
                    return true;

                // Check for FLV signature
                if (buffer[0] == 'F' && buffer[1] == 'L' && buffer[2] == 'V')
                    return true;

                // Check for #EXTM3U (HLS playlist)
                if (totalBytesRead >= 7)
                {
                    var start = System.Text.Encoding.ASCII.GetString(buffer, 0, Math.Min(totalBytesRead, 20));
                    if (start.TrimStart().StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // If we got binary data that's not HTML, assume it's a valid stream
            // Check if data looks binary (has non-printable characters)
            var nonPrintableCount = 0;
            for (var i = 0; i < Math.Min(totalBytesRead, 100); i++)
            {
                if (buffer[i] < 32 && buffer[i] != '\r' && buffer[i] != '\n' && buffer[i] != '\t')
                    nonPrintableCount++;
            }

            // If more than 10% non-printable, likely binary stream data
            if (nonPrintableCount > Math.Min(totalBytesRead, 100) / 10)
                return true;

            // Got some data but couldn't identify format - be conservative and accept it
            // if content-type was valid
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            return ValidStreamContentTypes.Contains(contentType);
        }
        catch
        {
            return false;
        }
    }
}
