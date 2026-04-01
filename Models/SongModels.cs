namespace SongConverter.Models;

public record SongInfo(string Title, string Subtitle);

public record SongDetail(string Title, string Subtitle, string? FullTitle, string? FolderTitle);
