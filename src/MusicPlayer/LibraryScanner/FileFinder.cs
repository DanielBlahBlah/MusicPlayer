using System.IO;
using Microsoft.EntityFrameworkCore;
using MusicPlayer.LibraryDatabase;
using MusicPlayer.LibraryObjects;

namespace MusicPlayer.LibraryScanner;

public static class FileFinder
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aa", ".aax", ".aac", ".aiff", ".aif", ".ape",
        ".dsf", ".dff", ".flac", ".m4a", ".m4b", ".m4p",
        ".mp3", ".mpc", ".mpp", ".ogg", ".oga", ".opus",
        ".wav", ".wma", ".wv", ".webm"
    };

    public static List<string> FindFiles(string folder) => 
        Directory.EnumerateFiles(folder,"*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            })
            .Where(p => Extensions.Contains(Path.GetExtension(p)))
            .Select(Path.GetFullPath)
            .ToList();
    
    public static async Task<(List<(string path, FileInfo info)> changed, int handled )> FindChangedAsync(
        MusicDatabase db,
        List<string> paths,
        ScanResult result,
        CancellationToken ct)
    {
        var known = await db.tracks.AsNoTracking()
            .Select(t => new { t.filePath, t.fileSize, t.lastWriteTime })
            .ToDictionaryAsync(
                t => t.filePath,
                t => (Size: t.fileSize, ticks: t.lastWriteTime),
                StringComparer.OrdinalIgnoreCase,
            ct);

        var changed = new List<(string path, FileInfo info)>();
        var handled = 0;

        foreach (var path in paths)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var info = new FileInfo(path);

                if (known.TryGetValue(path, out var k) &&
                    k.Size == info.Length &&
                    k.ticks == info.LastWriteTimeUtc.Ticks)
                {
                    handled++;
                }
                else changed.Add((path, info));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result.filesSkipped++;
                result.Errors.Add($"{path}: {ex.Message}");
                handled++;
            }
        }

        return (changed, handled);
    }
}