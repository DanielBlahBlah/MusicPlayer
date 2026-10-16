using Microsoft.EntityFrameworkCore;
using MusicPlayer.LibraryDatabase;
using MusicPlayer.LibraryObjects;

namespace MusicPlayer.LibraryScanner;

public sealed class AlbumResolver(MusicDatabase db)
{
    private readonly Dictionary<(string,string), int> cache = new();

    public async Task<int> ResolveAsync(MusicMetadata m, CancellationToken ct)
    {
        var artist = Normalise(m.albumArtist);
        var title = Normalise(m.albumTitle);
        var key = (artist, title);

        if (cache.TryGetValue(key, out var id)) return id;

        id = await db.albums
            .Where(a => a.normalisedArtist == artist && a.normalisedTitle == title)
            .Select(a => a.id)
            .FirstOrDefaultAsync(ct);

        if (id == 0)
        {
            var album = new Album
            {
                title = m.albumTitle,
                albumArtist = m.albumArtist,
                normalisedArtist = artist,
                normalisedTitle = title,
                replayGainAlbumGain = m.replayGainAlbumGain,
                replayGainAlbumPeak = m.replayGainAlbumPeak
            };

            db.albums.Add(album);
            await db.SaveChangesAsync(ct);
            id = album.id;
        }

        cache[key] = id;
        return id;
    }

    private static string Normalise(string? value) =>
        string.Join("", (value ?? "")
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
}