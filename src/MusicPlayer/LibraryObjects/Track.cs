namespace MusicPlayer.LibraryObjects;

public class Track
{
    public int id { get; set;}

    public int albumId { get; set;}
    public Album album { get; set;} = null!;

    public string filePath { get; set;} = "";
    public long fileSize { get; set;}
    public long lastWriteTime { get; set;}
    public DateTime dateAdded { get; set;} = DateTime.UtcNow;
    public string? artworkPath { get; set;}
    public string? artworkHash { get; set;}

    public string? title { get; set;} = "";
    public string? artist { get; set;} = "";
    public int? trackNumber { get; set;}
    public int? discNumber { get; set;}
    public string? composer { get; set;}
    public string? conductor { get; set;}
    public string? genre { get; set;}
    public string? comment { get; set;}
    public string? lyrics { get; set;}
    public string? grouping { get; set;}
    public int? bpm { get; set;}

    public string? albumTitle { get; set;}
    public string? albumArtist { get; set;}
    public string? albumArtistSort { get; set;}
    public string? releaseDate { get; set;}
    public int? releaseYear { get; set;}
    public string? publisher { get; set;}
    public bool isCompilation { get; set;}

    public long durationMs { get; set;}
    public int? bitrate { get; set;}
    public int? sampleRate { get; set;}
    public int? bitDepth { get; set;}
    public int? channels { get; set;}
    public string? codec { get; set;}
    public bool isLossless { get; set;}

    public double? replayGainTrackGain { get; set;}
    public double? replayGainTrackPeak { get; set;}

    public int listenCount { get; set;}
    public int skipCount { get; set;}
    public DateTime? lastPlayed { get; set;}
    public int? rating { get; set;}
    public bool isFavourite { get; set;}
}