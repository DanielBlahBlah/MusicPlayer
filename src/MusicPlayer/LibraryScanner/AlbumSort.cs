using System.Globalization;
using System.Text;

namespace MusicPlayer.LibraryScanner;

public static class AlbumSort
{
    private static readonly string[] ignoredWords = ["the ", "a ", "an "];

    public static string Letter(string? title)
    {
        foreach (var ch in sortKey(title).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;

            var upper = char.ToUpperInvariant(ch);
            return char.IsLetter(upper) ? upper.ToString() : "#";
        }

        return "#";
    }

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