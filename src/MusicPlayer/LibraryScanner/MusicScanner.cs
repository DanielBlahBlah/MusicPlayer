using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MusicPlayer.LibraryDatabase;
using MusicPlayer.LibraryObjects;

namespace MusicPlayer.LibraryScanner;

public sealed class MusicScanner(string settingsDirectory)
{
    private const int BatchSize = 200;

    public async Task<ScanResult> ScanAsync(
        string folder,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);

        var ct = cancellationToken;
        var result = new ScanResult();

        await using var db = MusicDatabase.CreateContext(settingsDirectory);
        await db.Database.EnsureCreatedAsync(ct);

        var found = FileFinder.FindFiles(folder);
        var foundSet = new HashSet<string>(found, StringComparer.OrdinalIgnoreCase);
        result.filesFound = found.Count;

        var (changed, handled) = await FileFinder.FindChangedAsync(db, found, result, ct);
        progress?.Report(handled);

        var touched = new HashSet<int>();
        var albums = new AlbumResolver(db);
        var artwork = new AlbumArtwork(db,settingsDirectory);

        IDbContextTransaction? tx = null;
        var inBatch = 0;

        try
        {
            foreach (var (path,info) in changed)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    tx ??= await db.Database.BeginTransactionAsync(ct);

                    var metadata = MusicMetadataReader.Read(path);
                    var albumId = await albums.ResolveAsync(metadata, ct);

                    var track = await db.tracks.FirstOrDefaultAsync(t => t.filePath == path, ct);
                    var isNew = track == null;

                    if (track == null)
                    {
                        track = new Track { filePath = path};
                        db.tracks.Add(track);
                    }
                    else touched.Add(track.albumId);
                    var cover = await artwork.GetCoverAsync(path, metadata.artwork, ct);
                    TrackMapper.Apply(track, metadata, info, albumId, cover);
                    await db.SaveChangesAsync(ct);

                    touched.Add(albumId);
                    await artwork.SetAlbumCoverAsync(albumId, cover, ct);
                    if (isNew) result.filesAdded++; else result.filesUpdated++;

                    if (++inBatch >= BatchSize)
                    {
                        await tx.CommitAsync(ct);
                        await tx.DisposeAsync();
                        tx = null;
                        inBatch = 0;
                        db.ChangeTracker.Clear();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    db.ChangeTracker.Clear();
                    result.filesSkipped++;
                    result.Errors.Add($"{path}: {ex.Message}");
                }

                progress?.Report(++handled);
            }
            if (tx != null) await tx.CommitAsync(ct);
        }
        finally
        {
            if (tx != null) await tx.DisposeAsync();
        }

        db.ChangeTracker.Clear();

        await LibraryPruner.PruneAsync(db, folder, foundSet, touched, result, ct);

        foreach (var albumId in touched) await AlbumRefresher.RefreshAsync(db, albumId, ct);

        await db.SaveChangesAsync(ct);

        return result;
    }
}