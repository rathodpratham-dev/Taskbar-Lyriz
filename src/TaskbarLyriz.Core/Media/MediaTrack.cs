namespace TaskbarLyriz.Core.Media;

public sealed record MediaTrack
{
    public static MediaTrack Empty { get; } = new();

    public string Title { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public string Album { get; init; } = string.Empty;

    public string AlbumArtist { get; init; } = string.Empty;

    public uint TrackNumber { get; init; }

    public MediaArtwork? Artwork { get; init; }

    public bool HasMetadata =>
        !string.IsNullOrWhiteSpace(Title) ||
        !string.IsNullOrWhiteSpace(Artist) ||
        !string.IsNullOrWhiteSpace(Album);
}

public sealed record MediaArtwork(byte[] Data, string? ContentType)
{
    public bool IsEmpty => Data.Length == 0;
}
