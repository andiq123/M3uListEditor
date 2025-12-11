using Core.Interfaces;

namespace Infrastructure.Services;

/// <summary>
/// Tests if streaming links are alive by validating response headers and content.
/// </summary>
public sealed class SignalTester : ISignalTester
{
    private readonly HttpClient _client;

    private static readonly HashSet<string> ValidStreamContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp2t", "video/mp4", "video/mpeg", "video/x-mpegurl", "video/x-ms-asf",
        "video/x-msvideo", "video/x-flv", "video/webm", "video/3gpp", "video/quicktime",
        "audio/mpeg", "audio/aac", "audio/mp4", "audio/x-mpegurl", "audio/x-scpls",
        "application/vnd.apple.mpegurl", "application/x-mpegurl", "application/dash+xml",
        "application/octet-stream", "binary/octet-stream"
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

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        try
        {
            return await TryGetRequestAsync(uri, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryGetRequestAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", "VLC/3.0.18 LibVLC/3.0.18");
            request.Headers.TryAddWithoutValidation("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Icy-MetaData", "1");

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!IsAcceptableStatusCode(response.StatusCode))
                return false;

            return await CanReadStreamDataAsync(response, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAcceptableStatusCode(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.OK => true,
            System.Net.HttpStatusCode.PartialContent => true,
            System.Net.HttpStatusCode.NoContent => false,
            _ => (int)statusCode >= 200 && (int)statusCode < 300
        };
    }

    private static async Task<bool> CanReadStreamDataAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

            var buffer = new byte[4096];
            var totalBytesRead = 0;
            var attempts = 0;

            while (attempts < 3 && totalBytesRead < 512)
            {
                try
                {
                    var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalBytesRead, Math.Min(1024, buffer.Length - totalBytesRead)), timeoutCts.Token);
                    if (bytesRead == 0) break;
                    totalBytesRead += bytesRead;
                    if (totalBytesRead >= 64) break;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    attempts++;
                    if (totalBytesRead > 0) break;
                }
                attempts++;
            }

            if (totalBytesRead == 0)
                return false;

            if (totalBytesRead >= 10)
            {
                var header = System.Text.Encoding.ASCII.GetString(buffer, 0, Math.Min(totalBytesRead, 200));
                var trimmedHeader = header.TrimStart();

                if (trimmedHeader.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                    trimmedHeader.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                    (trimmedHeader.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && trimmedHeader.Contains("<html", StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                if (trimmedHeader.StartsWith("404", StringComparison.OrdinalIgnoreCase) ||
                    trimmedHeader.StartsWith("403", StringComparison.OrdinalIgnoreCase) ||
                    trimmedHeader.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                    trimmedHeader.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                    trimmedHeader.Contains("access denied", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (totalBytesRead >= 3)
            {
                if (buffer[0] == 0x47) return true;
                if (buffer[0] == 'I' && buffer[1] == 'D' && buffer[2] == '3') return true;
                if (buffer[0] == 0xFF && (buffer[1] & 0xE0) == 0xE0) return true;
                if (buffer[0] == 0xFF && (buffer[1] & 0xF0) == 0xF0) return true;
                if (buffer[0] == 'F' && buffer[1] == 'L' && buffer[2] == 'V') return true;

                if (totalBytesRead >= 7)
                {
                    var start = System.Text.Encoding.ASCII.GetString(buffer, 0, Math.Min(totalBytesRead, 20));
                    if (start.TrimStart().StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            var nonPrintableCount = 0;
            for (var i = 0; i < Math.Min(totalBytesRead, 100); i++)
            {
                if (buffer[i] < 32 && buffer[i] != '\r' && buffer[i] != '\n' && buffer[i] != '\t')
                    nonPrintableCount++;
            }

            if (nonPrintableCount > Math.Min(totalBytesRead, 100) / 10)
                return true;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            return ValidStreamContentTypes.Contains(contentType);
        }
        catch
        {
            return false;
        }
    }
}
