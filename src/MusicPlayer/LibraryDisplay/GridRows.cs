using System.ComponentModel;
using MusicPlayer.LibraryObjects;

namespace MusicPlayer;

public abstract class GridRow
{
}

public sealed class HeaderRow(string letter) : GridRow
{
    public string letter {get;} = letter;
}

public sealed class AlbumRow : GridRow, INotifyPropertyChanged
{
    private int _columns;

    public AlbumRow(IReadOnlyList<Album> albums, int columns)
    {
        this.albums = albums;
        _columns = columns;
    }

    public IReadOnlyList<Album> albums { get;}

    public int columns
    {
        get => _columns;
        set
        {
            if (_columns == value) return;
            _columns = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(columns)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class DetailRow : GridRow, INotifyPropertyChanged
{
    public DetailRow(AlbumDetail detail, int pointerColumn, int columns, int trackColumnCount)
    {
        this.detail = detail;
        this.pointerColumn = pointerColumn;
        this.columns = columns;
        this.trackColumnCount = trackColumnCount;
        trackSections = detail.Layout(trackColumnCount);
    }

    public AlbumDetail detail { get;}
    public int pointerColumn { get; private set;}
    public int columns { get; private set;}
    public int trackColumnCount { get; private set;}
    public IReadOnlyList<TrackSection> trackSections { get; private set;}

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateFrom(DetailRow other)
    {
        if (pointerColumn != other.pointerColumn)
        {
            pointerColumn = other.pointerColumn;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(pointerColumn)));
        }

        if (columns != other.columns)
        {
            columns = other.columns;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(columns)));
        }

        if (sameShape(trackSections, other.trackSections)) return;

        trackColumnCount = other.trackColumnCount;
        trackSections = other.trackSections;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(trackColumnCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(trackSections)));
    }

    private static bool sameShape(IReadOnlyList<TrackSection> a, IReadOnlyList<TrackSection> b) =>
        a.Count == b.Count &&
        a.Zip(b).All(p =>
            p.First.cells == p.Second.cells &&
            p.First.columns.Count == p.Second.columns.Count &&
            p.First.columns.Zip(p.Second.columns).All(c => c.First.Count == c.Second.Count));
}