namespace MusicPlayer.LibraryScanner;

public static class AlbumSort
{
    private static readonly string[] ignoredWords = ["the ", "a ", "an "];

    public static string sortKey(string? text)
    {
        var value = (text ?? "").Trim();

        foreach (var word in ignoredWords)
        {
            if(value.Length > word.Length && value.StartsWith(word, StringComparison.CurrentCultureIgnoreCase))
            {
                return value[word.Length..].TrimStart();
            }
        }

        return value;
    }
}