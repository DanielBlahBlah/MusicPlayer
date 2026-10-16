using System.IO;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MusicPlayer.LibraryDatabase;

namespace MusicPlayer.LibraryScanner;

public sealed record Cover(string path, string? hash, bool fromFolder);

public sealed class AlbumArtwork(MusicDatabase db, string settingsDirectory)
{
    private static readonly string[] FolderNames = ["cover", "folder", "front","albumart","album"];

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg",".jpeg",".png"};

    private readonly string coverDirectory = Path.Combine(settingsDirectory, "covers");
    private readonly Dictionary<string,string?> folderCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> hasFolderCover = new();
    private readonly HashSet<int> hasAnyCover = new();

    public async Task<Cover?> GetCoverAsync(string audioPath, byte[]? embedded, CancellationToken ct)
    {
        var folderCover = FindFolderCover(audioPath);
        if (folderCover != null) return new Cover(folderCover, null, true);

        return await SaveEmbeddedAsync(embedded, ct);
    }

    private string? FindFolderCover(string audioPath)
    {
        var folder = Path.GetDirectoryName(audioPath);
        if (folder == null) return null;

        if (folderCache.TryGetValue(folder, out var cached)) return cached;

        string? found = null;

        try
        {
            var images = Directory.EnumerateFiles(folder)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
                .ToList();

            foreach (var name in FolderNames)
            {
                found = images.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase));

                if (found != null) break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            
        }

        folderCache[folder] = found;

        return found;
    }

    private async Task<Cover?> SaveEmbeddedAsync(byte[]? data, CancellationToken ct)
    {
        if (data == null) return null;

        try
        {
            Directory.CreateDirectory(coverDirectory);

            var hash = Convert.ToHexString(SHA256.HashData(data));
            var path = Path.Combine(coverDirectory, hash + Extension(data));

            if (!File.Exists(path)) await File.WriteAllBytesAsync(path, data, ct);

            return new Cover(path, hash, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task SetAlbumCoverAsync(int albumId, Cover? cover, CancellationToken ct)
    {
        if (cover == null || hasFolderCover.Contains(albumId)) return;
        if (!cover.fromFolder && hasAnyCover.Contains(albumId)) return;

        var current = await db.albums
            .Where(a => a.id == albumId)
            .Select(a => a.artworkPath)
            .FirstOrDefaultAsync(ct);

        var prefix = coverDirectory + Path.DirectorySeparatorChar;
        var currentIsFolder = current != null && !current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        if (currentIsFolder)
        {
            hasFolderCover.Add(albumId);
            return;
        }

        if (current != null && !cover.fromFolder)
        {
            hasAnyCover.Add(albumId);
            return;
        }

        await db.albums
            .Where(a => a.id == albumId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.artworkPath, cover.path)
                .SetProperty(a => a.artworkHash, cover.hash), ct);

        if (cover.fromFolder) hasFolderCover.Add(albumId);
        else hasAnyCover.Add(albumId);
    }

    private static string Extension(byte[] data) =>
        data.Length > 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
            ? ".png"
            : ".jpg";
}