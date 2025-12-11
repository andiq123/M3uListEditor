using System.Buffers;
using System.Text;
using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Services;

/// <summary>
/// Handles file system operations for M3U files with optimized I/O.
/// </summary>
public sealed class FileHandler : IFileHandler
{
    private const int DefaultBufferSize = 65536; // 64KB buffer for better I/O performance

    /// <inheritdoc />
    public void WriteChannels(string path, IEnumerable<Channel> channels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(channels);

        var content = BuildM3uContent(channels);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content, Encoding.UTF8);
    }

    /// <inheritdoc />
    public async Task WriteChannelsAsync(string path, IEnumerable<Channel> channels, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(channels);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Use StreamWriter for better memory efficiency with large files
        await using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            DefaultBufferSize,
            FileOptions.Asynchronous);
        await using var writer = new StreamWriter(stream, Encoding.UTF8, DefaultBufferSize);

        await writer.WriteLineAsync("#EXTM3U");

        foreach (var channel in channels)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Write EXTINF line with proper formatting
            await writer.WriteLineAsync(FormatExtinfLine(channel));

            // Write group if present
            if (!string.IsNullOrEmpty(channel.GroupName))
            {
                await writer.WriteLineAsync(FormatGroupLine(channel.GroupName));
            }

            // Write link
            await writer.WriteLineAsync(channel.Link);
        }
    }

    /// <inheritdoc />
    public void DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // File might be in use, ignore
        }
        catch (UnauthorizedAccessException)
        {
            // No permission, ignore
        }
    }

    /// <inheritdoc />
    public void EnsureDirectoryExists(string path)
    {
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// Formats the EXTINF line properly for M3U output.
    /// </summary>
    private static string FormatExtinfLine(Channel channel)
    {
        // If the name already starts with #EXTINF, return as-is
        if (channel.Name.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
        {
            return channel.Name;
        }

        // Otherwise, create a proper EXTINF line
        return $"#EXTINF:-1,{channel.Name}";
    }

    /// <summary>
    /// Formats the group line properly for M3U output.
    /// </summary>
    private static string FormatGroupLine(string groupName)
    {
        // If already formatted as EXTGRP, return as-is
        if (groupName.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase))
        {
            return groupName;
        }

        return $"#EXTGRP:{groupName}";
    }

    /// <summary>
    /// Builds M3U file content from channels (for sync write).
    /// </summary>
    private static string BuildM3uContent(IEnumerable<Channel> channels)
    {
        var channelList = channels as IReadOnlyList<Channel> ?? channels.ToList();

        // Estimate capacity: header + (extinf + group + link + newline) per channel
        var estimatedSize = 10 + (channelList.Count * 200);
        var sb = new StringBuilder(estimatedSize);

        sb.AppendLine("#EXTM3U");

        foreach (var channel in channelList)
        {
            sb.AppendLine(FormatExtinfLine(channel));

            if (!string.IsNullOrEmpty(channel.GroupName))
            {
                sb.AppendLine(FormatGroupLine(channel.GroupName));
            }

            sb.AppendLine(channel.Link);
        }

        return sb.ToString();
    }
}
