namespace MusicPlayer;

public class Album
{
    public string title { get; set;} = "";
    public string artist{ get; set;} = "";
    public string coverPath {get; set;} = "";
    public string genre{ get; set;} = "";
    public List<String> tracks {get; set;} = new();
}