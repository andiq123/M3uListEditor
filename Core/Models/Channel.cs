namespace Core.Models;

/// <summary>
/// Represents a channel in an M3U playlist.
/// </summary>
/// <param name="Id">Unique identifier for the channel.</param>
/// <param name="Name">Display name/EXTINF line of the channel.</param>
/// <param name="Link">URL/stream link for the channel.</param>
/// <param name="GroupName">Optional group name for the channel.</param>
public record Channel(int Id, string Name, string Link, string GroupName = "");