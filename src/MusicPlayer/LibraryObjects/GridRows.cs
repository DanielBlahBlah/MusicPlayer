using MusicPlayer.LibraryObjects;

namespace MusicPlayer;

public abstract class GridRow
{
}

public sealed class HeaderRow(string letter) : GridRow
{
    public string letter {get;} = letter;
}

public sealed class AlbumRow(IReadOnlyList<Album> albums) : GridRow
{
    public IReadOnlyList<Album> albums {get;} = albums;
}