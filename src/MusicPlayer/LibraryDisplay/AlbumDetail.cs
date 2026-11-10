using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using MusicPlayer.LibraryObjects;

namespace MusicPlayer;

public sealed class AlbumDetail(Album album, string summary, IReadOnlyList<DiscSection> discs)
{
    private const int maxColumns = 4;
    private const int minTracksPerColumn = 4;

    private const double numberColumn = 34;
    private const double lengthColumn = 70;
    private const double columnGap = 8;
    private const double columnSafety = 16;
    private const double wideGap = 0;
    private const double tightGap = 0;
    private const double trackFontSize = 15;

    public Album album { get;} = album;
    public string summary { get;} = summary;
    public IReadOnlyList<DiscSection> discs { get;} = discs;

    public static AlbumDetail Build(Album album, IReadOnlyList<Track> tracks)
    {
        var byDisc = tracks.GroupBy(t => t.discNumber ?? 1).OrderBy(g => g.Key).ToList();
        var multiDisc = byDisc.Count > 1;

        var discs = byDisc
            .Select(g => new DiscSection(
                    multiDisc ? $"Disc {g.Key}" : null,
                    g.Select(t => TrackRow.From(t, album)).ToList()))
                .ToList();

        var parts = new List<string>();
        if (album.releaseYear != null) parts.Add(album.releaseYear.Value.ToString());
        if (!string.IsNullOrWhiteSpace(album.genre)) parts.Add(album.genre);
        parts.Add(tracks.Count == 1 ? "1 track" : $"{tracks.Count} tracks");
        parts.Add(FormatTotal(TimeSpan.FromMilliseconds(tracks.Sum(t => t.durationMs))));

        return new AlbumDetail (album, string.Join(" · ", parts), discs);
    }

    private static readonly Typeface trackFont = new(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    public static double Measure(string text)
    {
        var pixelsPerDip = Application.Current?.MainWindow is {} window
            ? VisualTreeHelper.GetDpi(window).PixelsPerDip
            : 1.0;

        var formatted = new FormattedText(
            text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            trackFont, trackFontSize, Brushes.White, pixelsPerDip);

        return formatted.WidthIncludingTrailingWhitespace;
    }

    private double longestLabelWidth() => discs
        .SelectMany(d => d.tracks)
        .Select(t => t.labelWidth)
        .DefaultIfEmpty(0)
        .Max();

    public int ChooseColumns(double availableWidth)
    {
        var slotWidth = numberColumn + longestLabelWidth() + lengthColumn + columnGap + columnSafety;

        return Math.Clamp((int)(Math.Floor(availableWidth) / slotWidth), 1, maxColumns);
    }

    public IReadOnlyList<TrackSection> Layout(int slots)
    {
        var longest = discs.Count == 0 ? 0 : discs.Max(d => d.tracks.Count);
        var columns = Math.Clamp(longest/minTracksPerColumn, 1, Math.Max(1, slots));
        var perColumn = Math.Max(1, (int)Math.Ceiling(longest/(double)columns));

        var gap = columns >= 3 ? tightGap : wideGap;
        var margin = new Thickness(0, 0, gap,0);

        foreach (var track in discs.SelectMany(d => d.tracks)) track.rowMargin = margin;

        var growTo = columns == 1
            ? numberColumn + longestLabelWidth() + lengthColumn + columnGap + gap + columnSafety
            : 0;

        return discs
            .Select(disc => new TrackSection(disc.heading, SplitIntoColumns(disc.tracks, perColumn), columns, growTo))
            .ToList();
    }

    private static IReadOnlyList<IReadOnlyList<TrackRow>> SplitIntoColumns(IReadOnlyList<TrackRow> tracks, int perColumn) =>
        tracks
        .Chunk(perColumn)
        .Select(column => (IReadOnlyList<TrackRow>)column.ToList())
        .ToList();

    public static string FormatDuration(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(milliseconds);

        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }

    private static string FormatTotal(TimeSpan total) => total.TotalHours >= 1
        ? $"{(int)total.TotalHours} hr {total.Minutes} min"
        : $"{Math.Max(1, (int)Math.Round(total.TotalMinutes))} min";
}

public sealed class DiscSection(string? heading, IReadOnlyList<TrackRow> tracks)
{
    public string? heading { get;} = heading;
    public IReadOnlyList<TrackRow> tracks { get;} = tracks;
}

public sealed class TrackSection(string? heading, IReadOnlyList<IReadOnlyList<TrackRow>> columns, int cells, double growTo)
{
    public double growTo { get;} = growTo;
    public int cells { get;} = cells;
    public int slots { get;} = cells == 1 ? 2 : cells;
    public string heading { get;} = heading ?? "";
    public Visibility headingVisibility { get;} = heading == null ? Visibility.Collapsed : Visibility.Visible;
    public IReadOnlyList<IReadOnlyList<TrackRow>> columns { get;} = columns;
}

public sealed class TrackRow
{
    public string number { get; init;} = "";
    public string title { get; init;} = "";
    public string artist { get; init;} = "";
    public string artistLabel => artist == "" ? "" : " " + artist;
    public string duration { get; init;} = "";
    public double labelWidth { get; private set;}
    public Thickness rowMargin { get; set;}

    public static TrackRow From(Track track, Album album)
    {
        var sameArtist = string.IsNullOrWhiteSpace(track.artist) || string.Equals(track.artist, album.albumArtist, StringComparison.OrdinalIgnoreCase);

        var row = new TrackRow
        {
            number = track.trackNumber?.ToString() ?? "",
            title = track.title ?? Path.GetFileNameWithoutExtension(track.filePath),
            artist = sameArtist ? "" : track.artist!,
            duration = FormatDuration(track.durationMs)
        };

        row.labelWidth = AlbumDetail.Measure(row.title + row.artistLabel);
        return row;
    }

    private static string FormatDuration(long milliseconds) => AlbumDetail.FormatDuration(milliseconds);
}