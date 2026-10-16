using System.IO;
using Microsoft.EntityFrameworkCore;
using MusicPlayer.LibraryDatabase;
using MusicPlayer.LibraryObjects;

namespace MusicPlayer.LibraryScanner;

public static class LibraryPruner
{
    public static async Task PruneAsync(
        MusicDatabase db,
        string folder,
        HashSet<string> found,
        HashSet<int> touched,
        ScanResult result,
        CancellationToken ct)
    {
        if (found.Count == 0) return;

        var root = Path.GetFullPath(folder);

        if (!root.EndsWith(Path.DirectorySeparatorChar)) root += Path.DirectorySeparatorChar;

        var inFolder = await db.tracks.AsNoTracking()
            .Where(t => t.filePath.StartsWith(root))
            .Select(t => new { t.id, t.albumId, t.filePath})
            .ToListAsync(ct);
        
        var missing = inFolder.Where(t => !found.Contains(t.filePath)).ToList();

        foreach (var t in missing) touched.Add(t.albumId);

        foreach (var ids in missing.Select(t => t.id).Chunk(500)) await db.tracks.Where( t=> ids.Contains(t.id)).ExecuteDeleteAsync(ct);

        result.filesRemoved += missing.Count;

        await db.albums.Where(a => !a.tracks.Any()).ExecuteDeleteAsync(ct);
    }
}