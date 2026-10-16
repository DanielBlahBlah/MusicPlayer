namespace MusicPlayer.LibraryObjects;

public sealed class ScanResult
{
    public int filesFound { get; set;}
    public int filesAdded { get; set;}
    public int filesUpdated { get; set;}
    public int filesSkipped { get; set;}
    public int filesRemoved { get; set;}

    public List<string> Errors { get;} = [];
}