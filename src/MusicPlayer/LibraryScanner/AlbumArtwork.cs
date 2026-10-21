using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MusicPlayer.LibraryDatabase;
using SkiaSharp;

namespace MusicPlayer.LibraryScanner;

public sealed record Cover(string path, string? hash);

public static class CoverPaths
{
    public const int smallSize = 200;
    public const int mediumSize = 350;

    public const int jpegQuality = 92;
    public const float sharpenAmount = 0.05f;

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

    public async Task RepairAsync(Action onProgress, CancellationToken ct)
    {
        var rows = await db.albums
            .AsNoTracking()
            .Where(a => a.artworkHash != null && a.artworkPath != null)
            .Select(a => new {a.id, a.title, a.artworkPath, a.artworkHash})
            .ToListAsync(ct);

        var albums = rows
            .OrderBy(a => AlbumSort.sortKey(a.title), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.id)
            .ToList();

        var sinceReport = Stopwatch.StartNew();
        var pending = false;

        foreach (var album in albums)
        {
            ct.ThrowIfCancellationRequested();

            var hash = album.artworkHash!;
            var small = CoverPaths.Small(settingsDirectory, hash)!;
            var medium = CoverPaths.Medium(settingsDirectory, hash)!;

            if (File.Exists(small) && File.Exists(medium)) continue;
            if (thumbnailsMade.Contains(hash)) continue;

            try
            {
                var source = album.artworkPath!;
                var data = IsImage(source) ? File.ReadAllBytes(source) : MusicMetadataReader.ReadEmbeddedArtwork(source);

                if (data != null && MakeThumbnails(data, hash)) pending = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                
            }

            if (pending && sinceReport.ElapsedMilliseconds >= 250)
            {
                onProgress();
                pending = false;
                sinceReport.Restart();
            }
        }

        if (pending) onProgress();
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
            using var source = SKBitmap.Decode(data);

            if (source == null) return false;

            WriteThumbnail(source, CoverPaths.Small(settingsDirectory, hash)!, CoverPaths.smallSize);

            WriteThumbnail(source, CoverPaths.Medium(settingsDirectory, hash)!, CoverPaths.mediumSize);

            thumbnailsMade.Add(hash);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void WriteThumbnail(SKBitmap source, string outPath, int size)
    {
        if (File.Exists(outPath)) return;

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        var scale = Math.Min(1.0, (double)size / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var info = new SKImageInfo(width, height);
        var shrinking = width < source.Width || height < source.Height;

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var resized = shrinking ? ResizeSmooth(source, width, height) : null;

        var a = CoverPaths.sharpenAmount;
        float[] kernel = [0, -a, 0, -a, 1+4*a, -a, 0, -a, 0];

        using var sharpen = shrinking && a > 0
            ? SKImageFilter.CreateMatrixConvolution(
                new SKSizeI(3,3), kernel, 1f, 0f, new SKPointI(1,1), SKShaderTileMode.Clamp, false)
            : null;
        
        using var paint = new SKPaint { ImageFilter = sharpen};

        canvas.DrawBitmap(resized ?? source, 0, 0, SKSamplingOptions.Default, paint);

        using var pixmap = surface.PeekPixels();
        using var output = new SKFileWStream(outPath);
        pixmap.Encode(output, new SKJpegEncoderOptions(CoverPaths.jpegQuality, SKJpegEncoderDownsample.Downsample444, SKJpegEncoderAlphaOption.Ignore));
    }

    private static SKBitmap ResizeSmooth(SKBitmap source, int width, int height)
    {
        var current = source;

        while (current.Width >= width * 2 && current.Height >= height * 2)
        {
            var half = current.Resize(current.Info.WithSize(current.Width/2, current.Height/2),
                new SKSamplingOptions(SKFilterMode.Linear))
                ?? throw new InvalidOperationException("Resize failed");

            if (!ReferenceEquals(current, source)) current.Dispose();
            current = half;
        }

        if (current.Width == width && current.Height == height) return current;

        var final = current.Resize(current.Info.WithSize(width,height),
            new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException("Resize failed");

        if (!ReferenceEquals(current, source)) current.Dispose();
        return final;
    }
}