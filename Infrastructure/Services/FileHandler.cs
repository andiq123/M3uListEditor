using System.Text;
using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Services;

/// <summary>
/// Handles file system operations for M3U files with optimized I/O.
/// </summary>
public sealed class FileHandler : IFileHandler
{
    private const int BufferSize = 65536;

    /// <inheritdoc />
    public void WriteChannels(string path, IEnumerable<Channel> channels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(channels);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, BuildM3uContent(channels), Encoding.UTF8);
    }

    /// <inheritdoc />
    public async Task WriteChannelsAsync(string path, IEnumerable<Channel> channels, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(channels);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous);
        await using var writer = new StreamWriter(stream, Encoding.UTF8, BufferSize);

        await writer.WriteLineAsync("#EXTM3U");

        foreach (var channel in channels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(FormatExtinfLine(channel));

            if (!string.IsNullOrEmpty(channel.GroupName))
                await writer.WriteLineAsync(FormatGroupLine(channel.GroupName));

            await writer.WriteLineAsync(channel.Link);
        }
    }

    /// <inheritdoc />
    public void DeleteFileIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <inheritdoc />
    public void EnsureDirectoryExists(string path)
    {
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private static string FormatExtinfLine(Channel channel) =>
        channel.Name.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase)
            ? channel.Name
            : $"#EXTINF:-1,{channel.Name}";

    private static string FormatGroupLine(string groupName) =>
        groupName.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase)
            ? groupName
            : $"#EXTGRP:{groupName}";

    private static string BuildM3uContent(IEnumerable<Channel> channels)
    {
        var channelList = channels as IReadOnlyList<Channel> ?? channels.ToList();
        var sb = new StringBuilder(10 + channelList.Count * 200);

        sb.AppendLine("#EXTM3U");

        foreach (var channel in channelList)
        {
            sb.AppendLine(FormatExtinfLine(channel));
            if (!string.IsNullOrEmpty(channel.GroupName))
                sb.AppendLine(FormatGroupLine(channel.GroupName));
            sb.AppendLine(channel.Link);
        }

        return sb.ToString();
    }
}
