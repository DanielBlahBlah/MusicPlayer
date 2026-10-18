using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using MusicPlayer.LibraryDatabase;
using SkiaSharp;

namespace MusicPlayer.LibraryScanner;

public sealed record Cover(string path, string? hash);

public static class CoverPaths
{
    public const int smallDivisor = 6;
    public const int smallMin = 256;

    public const int mediumDivisor = 4;
    public const int mediumMin = 512;

    public const int jpegQuality = 92;
    public const float sharpenAmount = 0.3f;

    public static string? Small(string settingsDirectory, string? hash) => hash == null ? null : Path.Combine(settingsDirectory, "covers", "small", hash + ".jpg");

    public static string? Medium(string settingsDirectory, string? hash) => hash == null ? null : Path.Combine(settingsDirectory, "covers", "medium", hash + ".jpg");
}

public sealed class AlbumArtwork(MusicDatabase db, string settingsDirectory)
{
    private static readonly string[] FolderNames = ["cover", "folder", "front","albumart","album"];

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg",".jpeg",".png"};

    private readonly Dictionary<string, Cover?> folderCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> thumbnailsMade = new();
    private readonly HashSet<int> hasFolderCover = new();
    private readonly HashSet<int> hasAnyCover = new();

    private static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    public Task<Cover?> GetCoverAsync(string audioPath, byte[]? embedded, CancellationToken ct) => Task.FromResult(GetCover(audioPath, embedded));

    private Cover? GetCover(string audioPath, byte[]? embedded)
    {
        var folder = Path.GetDirectoryName(audioPath);

        if (folder != null)
        {
            if (!folderCache.TryGetValue(folder, out var folderCover))
            {
                folderCover = BuildFolderCover(folder);
                folderCache[folder] = folderCover;
            }

            if (folderCover != null) return folderCover;
        }

        if (embedded == null) return null;

        var hash = Convert.ToHexString(SHA256.HashData(embedded));
        return MakeThumbnails(embedded, hash) ? new Cover(audioPath, hash) : null;
    }

    public async Task SetAlbumCoverAsync(int albumId, Cover? cover, CancellationToken ct)
    {
        if (cover == null || hasFolderCover.Contains(albumId)) return;

        var isFolderImage = IsImage(cover.path);
        if (!isFolderImage && hasAnyCover.Contains(albumId)) return;

        var current = await db.albums
            .Where(a => a.id == albumId)
            .Select(a => a.artworkPath)
            .FirstOrDefaultAsync(ct);

        if (current != null && IsImage(current))
        {
            hasFolderCover.Add(albumId);
            return;
        }

        if (current != null && !isFolderImage)
        {
            hasAnyCover.Add(albumId);
            return;
        }

        await db.albums
            .Where(a => a.id == albumId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.artworkPath, cover.path)
                .SetProperty(a => a.artworkHash, cover.hash), ct);

        if (isFolderImage) hasFolderCover.Add(albumId);
        else hasAnyCover.Add(albumId);
    }

    private Cover? BuildFolderCover(string folder)
    {
        var image = FindFolderImage(folder);
        if (image == null) return null;

        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(image.ToLowerInvariant())));

            return MakeThumbnails(File.ReadAllBytes(image), hash) ? new Cover(image, hash) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? FindFolderImage(string folder)
    {
        try
        {
            var images = Directory.EnumerateFiles(folder)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
                .ToList();

            foreach (var name in FolderNames)
            {
                var found = images.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase));

                if (found != null) return found;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            
        }

        return null;
    }

    private bool MakeThumbnails(byte[] data, string hash)
    {
        if (thumbnailsMade.Contains(hash)) return true;

        try
        {
            var original = BitmapFrame.Create(new MemoryStream(data), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).PixelWidth;

            WriteThumbnail(data, original, CoverPaths.Small(settingsDirectory, hash)!, CoverPaths.smallDivisor, CoverPaths.smallMin);

            WriteThumbnail(data, original, CoverPaths.Medium(settingsDirectory, hash)!, CoverPaths.mediumDivisor, CoverPaths.mediumMin);

            thumbnailsMade.Add(hash);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void WriteThumbnail(byte[] data, int original, string outPath, int divisor, int min)
    {
        if (File.Exists(outPath)) return;

        var width = Math.Min(original, Math.Max(min, original/divisor));

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = width;
        bitmap.StreamSource = new MemoryStream(data);
        bitmap.EndInit();
        bitmap.Freeze();

        var encoder = new JpegBitmapEncoder { QualityLevel = 85};

        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(outPath);
        encoder.Save(stream);
    }
}