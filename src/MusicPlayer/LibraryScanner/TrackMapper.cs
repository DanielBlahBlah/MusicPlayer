using System.IO;
using MusicPlayer.LibraryObjects;

namespace MusicPlayer.LibraryScanner;

public static class TrackMapper
{
    public static void Apply(Track t, MusicMetadata m, FileInfo info, int albumID, Cover? cover)
    {
        t.albumId = albumID;

        t.artworkPath = cover?.path;
        t.artworkHash = cover?.hash;

        t.fileSize = info.Length;
        t.lastWriteTime = info.LastWriteTimeUtc.Ticks;

        t.title = m.title;
        t.artist = m.artist;
        t.trackNumber = m.trackNumber;
        t.discNumber = m.discNumber;
        t.composer = m.composer;
        t.conductor = m.conductor;
        t.genre = m.genre;
        t.comment = m.comment;
        t.lyrics = m.lyrics;
        t.bpm = m.bpm;

        t.albumTitle = m.albumTitle;
        t.albumArtist = m.albumArtist;
        t.albumArtistSort = m.albumArtistSort;
        t.releaseDate = m.releaseDate;
        t.releaseYear = m.releaseYear;
        t.publisher = m.publisher;
        t.isCompilation = m.isCompilation;

        t.durationMs = m.durationMs;
        t.bitrate = m.bitrate;
        t.sampleRate = m.sampleRate;
        t.bitDepth = m.bitDepth;
        t.channels = m.channels;
        t.codec = m.codec;
        t.isLossless = m.isLossless;

        t.replayGainTrackGain = m.replayGainTrackGain;
        t.replayGainTrackPeak = m.replayGainTrackPeak;
    }
}