using System.Text;
using Core.Interfaces;

namespace Infrastructure.Services;

/// <summary>
/// Downloads files from remote URLs with streaming support and robust error handling.
/// </summary>
public sealed class FileDownloader : IFileDownloader
{
    private readonly HttpClient _client;
    private readonly IFileHandler _fileHandler;
    private readonly string _tempDirectory;
    private const int BufferSize = 81920;

    public FileDownloader(HttpClient client, IFileHandler fileHandler)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fileHandler = fileHandler ?? throw new ArgumentNullException(nameof(fileHandler));
        _tempDirectory = Path.Combine(Path.GetTempPath(), "M3uListEditor");
    }

    /// <inheritdoc />
    public async Task<string> DownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid URL: {url}", nameof(url));

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException($"URL must use HTTP or HTTPS protocol: {url}", nameof(url));

        _fileHandler.EnsureDirectoryExists(_tempDirectory);

        var fileName = GenerateFileName(uri);
        var filePath = Path.Combine(_tempDirectory, fileName);

        _fileHandler.DeleteFileIfExists(filePath);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (compatible; M3uListEditor/2.0)");
            request.Headers.TryAddWithoutValidation("Accept", "*/*");

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to download from {url}. Status: {response.StatusCode} ({(int)response.StatusCode})");

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous);

            var encoding = await DetectEncodingAsync(contentStream, cancellationToken);
            if (contentStream.CanSeek)
                contentStream.Position = 0;

            await using var writer = new StreamWriter(fileStream, Encoding.UTF8, BufferSize);
            using var reader = new StreamReader(contentStream, encoding, detectEncodingFromByteOrderMarks: true);

            var buffer = new char[BufferSize];
            int charsRead;

            while ((charsRead = await reader.ReadAsync(buffer, cancellationToken)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteAsync(buffer.AsMemory(0, charsRead), cancellationToken);
            }

            return filePath;
        }
        catch (HttpRequestException ex)
        {
            _fileHandler.DeleteFileIfExists(filePath);
            throw new InvalidOperationException($"Failed to download M3U file from {url}: {ex.Message}", ex);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _fileHandler.DeleteFileIfExists(filePath);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _fileHandler.DeleteFileIfExists(filePath);
            throw new TimeoutException($"Download timed out for {url}", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _fileHandler.DeleteFileIfExists(filePath);
            throw new InvalidOperationException($"Error downloading from {url}: {ex.Message}", ex);
        }
    }

    private static string GenerateFileName(Uri uri)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var originalName = Path.GetFileNameWithoutExtension(uri.LocalPath);

        if (!string.IsNullOrWhiteSpace(originalName) && originalName.Length is > 2 and < 50)
        {
            var sanitized = SanitizeFileName(originalName);
            if (!string.IsNullOrWhiteSpace(sanitized))
                return $"{sanitized}_{timestamp}.m3u";
        }

        return $"{SanitizeFileName(uri.Host.Replace("www.", ""))}_{timestamp}.m3u";
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);

        foreach (var c in name)
        {
            if (!invalidChars.Contains(c) && c != ' ')
                sb.Append(c);
            else if (c == ' ')
                sb.Append('_');
        }

        return sb.ToString();
    }

    private static async Task<Encoding> DetectEncodingAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (!stream.CanSeek)
            return Encoding.UTF8;

        var bom = new byte[4];
        var bytesRead = await stream.ReadAsync(bom.AsMemory(0, 4), cancellationToken);
        stream.Position = 0;

        if (bytesRead < 2) return Encoding.UTF8;
        if (bytesRead >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return Encoding.UTF8;
        if (bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode;
        if (bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;
        if (bytesRead >= 4 && bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF) return Encoding.UTF32;

        return Encoding.UTF8;
    }
}
