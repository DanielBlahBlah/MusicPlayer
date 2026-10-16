using System.IO;
using TagLib;

namespace MusicPlayer.LibraryScanner;

public static class MusicMetadataReader
{
    private static readonly Dictionary<string, (string codec, bool lossless)> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = ("MP3", false),
        [".aac"] = ("AAC", false),
        [".m4a"] = ("AAC", false),
        [".m4b"] = ("AAC", false),
        [".m4p"] = ("AAC", false),
        [".aa"] = ("Audible", false),
        [".aax"] = ("Audible", false),
        [".ogg"] = ("Vorbis", false),
        [".oga"] = ("Vorbis", false),
        [".opus"] = ("Opus", false),
        [".wma"] = ("WMA", false),
        [".webm"] = ("WebM", false),
        [".mpc"] = ("Musepack", false),
        [".mpp"] = ("Musepack", false),
        [".flac"] = ("FLAC", true),
        [".ape"] = ("APE", true),
        [".wv"] = ("WavPack", true),
        [".wav"] = ("WAV", true),
        [".aiff"] = ("AIFF", true),
        [".aif"] = ("AIFF", true),
        [".dsf"] = ("DSD", true),
        [".dff"] = ("DSD", true)
    };

    public static MusicMetadata Read(string path)
    {
        using var file = TagLib.File.Create(path);
        var tag = file.Tag;
        var props = file.Properties;

        var artist = Join(tag.Performers);
        var releaseDate = ReadReleaseDate(file);
        var isCompilation = ReadIsCompilation(file);
        var (codec, lossless) = DetectFormat(path, props);

        var albumArtist = Join(tag.AlbumArtists) ?? artist;
        if (Join(tag.AlbumArtists) == null && isCompilation) albumArtist = "Various Artists";

        var albumTitle = Clean(tag.Album)
            ?? Clean(Path.GetFileName(Path.GetDirectoryName(path)))
            ?? "Unkown Album";

        return new MusicMetadata
        {
            title = Clean(tag.Title),
            artist = artist,
            trackNumber = Positive(tag.Track),
            discNumber = Positive(tag.Disc),
            composer = Join(tag.Composers),
            conductor = Clean(tag.Conductor),
            genre = Join(tag.Genres),
            comment = Clean(tag.Comment),
            lyrics = ReadLrc(path) ?? Clean(tag.Lyrics),
            bpm = Positive(tag.BeatsPerMinute),

            albumTitle = albumTitle,
            albumArtist = albumArtist,
            albumArtistSort = Join(tag.AlbumArtistsSort),
            releaseDate = releaseDate,
            releaseYear = ParseYear(releaseDate),
            publisher = ReadPublisher(file),
            isCompilation = isCompilation,
            artwork = ReadArtwork(tag),

            durationMs = (long)props.Duration.TotalMilliseconds,
            bitrate = Positive(props.AudioBitrate),
            sampleRate = Positive(props.AudioSampleRate),
            bitDepth = Positive(props.BitsPerSample),
            channels = Positive(props.AudioChannels),
            codec = codec,
            isLossless = lossless,

            replayGainTrackGain = Finite(tag.ReplayGainTrackGain),
            replayGainTrackPeak = Finite(tag.ReplayGainTrackPeak),
            replayGainAlbumGain = Finite(tag.ReplayGainAlbumGain),
            replayGainAlbumPeak = Finite(tag.ReplayGainAlbumPeak)
        };
    }

    private static string? ReadReleaseDate(TagLib.File file)
    {
        string? raw = null;

        if (file.GetTag(TagLib.TagTypes.Id3v2) is TagLib.Id3v2.Tag id3) raw = Clean(id3.GetTextAsString("TDRC"));

        if (raw == null && file.GetTag(TagLib.TagTypes.Xiph) is TagLib.Ogg.XiphComment xiph) raw = Clean(xiph.GetField("DATE").FirstOrDefault());

        if (raw == null && file.Tag.Year > 0) raw = file.Tag.Year.ToString();

        return raw;
    }

    private static string? ReadPublisher(TagLib.File file)
    {
        if (file.GetTag(TagLib.TagTypes.Id3v2) is TagLib.Id3v2.Tag id3)
        {
            var value = Clean(id3.GetTextAsString("TPUB"));
            if (value != null) return value;
        }

        if (file.GetTag(TagLib.TagTypes.Xiph) is TagLib.Ogg.XiphComment xiph)
        {
            foreach (var key in new[] {" ORGANIZATION", "LABEL", "PUBLISHER" })
            {
                var value = Clean(xiph.GetField(key).FirstOrDefault());
                if (value != null) return value;
            }
        }

        return null;
    }

    private static bool ReadIsCompilation(TagLib.File file)
    {
        if (file.GetTag(TagLib.TagTypes.Id3v2) is TagLib.Id3v2.Tag id3 && Clean(id3.GetTextAsString("TCMP")) == "1") return true;

        if (file.GetTag(TagLib.TagTypes.Xiph) is TagLib.Ogg.XiphComment xiph && Clean(xiph.GetField("COMPILATION").FirstOrDefault()) == "1") return true;

        if (file.GetTag(TagLib.TagTypes.Apple) is TagLib.Mpeg4.AppleTag apple && apple.IsCompilation) return true;

        return false;
    }

    private static (string? codec, bool lossless) DetectFormat(string path, TagLib.Properties props)
    {
        var ext = Path.GetExtension(path);

        if (ext.Equals(".m4a", StringComparison.OrdinalIgnoreCase) || ext.Equals(".m4b", StringComparison.OrdinalIgnoreCase))
        {
            var description = props.Description ?? "";
            foreach (var c in props.Codecs) description += " " + c.Description;

            if (description.Contains("alac", StringComparison.OrdinalIgnoreCase)) return ("ALAC", true);
        }

        return Formats.TryGetValue(ext, out var format) ? format : (null, false);
    }

    private static string? ReadLrc(string path)
    {
        try
        {
            var lrcPath = Path.ChangeExtension(path, ".lrc");
            return System.IO.File.Exists(lrcPath) ? Clean (System.IO.File.ReadAllText(lrcPath)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static byte[]? ReadArtwork(TagLib.Tag tag)
    {
        var pictures = tag.Pictures;
        if (pictures == null || pictures.Length == 0) return null;

        var picture = pictures.FirstOrDefault(p => p.Type == TagLib.PictureType.FrontCover) ?? pictures[0];

        var data = picture.Data?.Data;

        return data is {Length: > 0} ? data : null;
    }

    private static int? ParseYear(string? text) =>
        text is { Length: >= 4 } &&
        int.TryParse(text.AsSpan(0,4),out var year) &&
        year > 0 ? year : null;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Join(string[]? values)
    {
        var parts = (values ?? [])
            .Select(Clean)
            .Where(v => v != null)
            .ToList();

        return parts.Count > 0 ? string.Join(": ", parts) : null;
    }

    private static int? Positive(uint value) => value > 0 ? (int)value : null;

    private static int? Positive(int value) => value > 0 ? value: null;

    private static double? Finite(double value) => double.IsNaN(value) || double.IsInfinity(value) ? null : value;
}