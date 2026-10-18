using System.ComponentModel.DataAnnotations.Schema;

namespace MusicPlayer.LibraryObjects;

public class Album
{
    public int id { get; set;}

    public string title{ get; set;} = "";
    public string? albumArtist{ get; set;}
    public string normalisedTitle{ get; set;} = "";
    public string normalisedArtist{ get; set;} = "";

    public string? artworkPath { get; set;}
    public string? artworkHash { get; set;}
    [NotMapped] public string? thumbnailPath { get; set;}
    [NotMapped] public string? mediumPath { get; set;}
    public DateTime dateAdded { get; set;} = DateTime.UtcNow;
    public int? rating { get; set;}

    public string? albumArtistSort { get; set;}
    public string? releaseDate { get; set;}
    public int? releaseYear { get; set;}
    public int? trackCount { get; set;}
    public int? discCount { get; set;}
    public string? publisher { get; set;}
    public bool isCompilation { get; set;}

    public double? replayGainAlbumGain { get; set;}
    public double? replayGainAlbumPeak { get; set;}

    public List<Track> tracks { get; set;} = [];
}