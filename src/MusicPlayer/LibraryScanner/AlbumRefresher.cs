using Microsoft.EntityFrameworkCore;
using MusicPlayer.LibraryDatabase;

namespace MusicPlayer.LibraryScanner;

public static class AlbumRefresher
{
    public static async Task RefreshAsync(MusicDatabase db, int albumId, CancellationToken ct)
    {
        var tracks = await db.tracks.AsNoTracking()
            .Where(t => t.albumId == albumId)
            .Select(t => new
            {
                t.discNumber,
                t.albumArtistSort,
                t.releaseDate,
                t.releaseYear,
                t.publisher,
                t.isCompilation
            })
            .ToListAsync(ct);

        if (tracks.Count == 0) return;

        var album = await db.albums.FirstOrDefaultAsync(a => a.id == albumId, ct);
        if (album == null) return;

        album.albumArtistSort = AgreedText(tracks.Select(t=> t.albumArtistSort));
        album.releaseDate = AgreedText(tracks.Select(t => t.releaseDate));
        album.releaseYear = AgreedInt(tracks.Select(t => t.releaseYear));
        album.publisher = AgreedText(tracks.Select(t => t.publisher));
        album.isCompilation = tracks.Any(t => t.isCompilation);

        album.trackCount = tracks.Count;
        album.discCount = tracks.Max(t => t.discNumber ?? 1);
    }

    private static string? AgreedText(IEnumerable<string?> values)
    {
        var distinct = values.Where(v => v != null).Distinct().Take(2).ToList();
        return distinct.Count == 1 ? distinct[0] : null;
    }

    private static int? AgreedInt(IEnumerable<int?> values)
    {
        var distinct = values.Where(v => v != null).Distinct().Take(2).ToList();
        return distinct.Count == 1 ? distinct[0] : null;
    }
}