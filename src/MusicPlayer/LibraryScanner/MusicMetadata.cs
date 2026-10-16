namespace MusicPlayer.LibraryScanner;

public sealed record MusicMetadata
{
    public string? title { get; init;}
    public string? artist { get; init;}
    public int? trackNumber { get; init;}
    public int? discNumber { get; init;}
    public string? composer { get; init;}
    public string? conductor { get; init;}
    public string? genre { get; init;}
    public string? comment { get; init;}
    public string? lyrics { get; init;}
    public int? bpm { get; init;}

    public string albumTitle { get; init;} = "";
    public string? albumArtist { get; init;}
    public string? releaseDate { get; init;}
    public string? albumArtistSort { get; init;}
    public int? releaseYear { get; init;}
    public string? publisher { get; init;}
    public bool isCompilation { get; init;}
    public byte[]? artwork { get; init;}

    public long durationMs { get; init;}
    public int? bitrate { get; init;}
    public int? sampleRate { get; init;}
    public int? bitDepth { get; init;}
    public int? channels { get; init;}
    public string? codec { get; init;}
    public bool isLossless { get; init;}

    public double? replayGainTrackGain { get; init;}
    public double? replayGainTrackPeak { get; init;}
    public double? replayGainAlbumGain { get; init;}
    public double? replayGainAlbumPeak { get; init;}
}