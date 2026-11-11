using System.Collections.ObjectModel;
using MusicPlayer.LibraryObjects;

namespace MusicPlayer.Player;

public sealed class Queue
{
    public ObservableCollection<Track> tracks { get;} = new();

    public void Enqueue(IEnumerable<Track> items)
    {
        foreach (var track in items) tracks.Add(track);
    }

    public void Clear() => tracks.Clear();
}