using System.IO;
using Microsoft.EntityFrameworkCore;
using MusicPlayer.LibraryObjects;

namespace MusicPlayer.LibraryDatabase;

public sealed class MusicDatabase(DbContextOptions<MusicDatabase> options) : DbContext(options)
{
    public DbSet<Album> albums => Set<Album>();
    public DbSet<Track> tracks => Set<Track>();

    public static MusicDatabase CreateContext(string settingsDirectory)
    {
        Directory.CreateDirectory(settingsDirectory);

        var dbPath = Path.Combine(settingsDirectory, "library.db");

        var options = new DbContextOptionsBuilder<MusicDatabase>()
            .UseSqlite($"Data Source = {dbPath}")
            .Options;

        return new MusicDatabase(options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var album = modelBuilder.Entity<Album>();

        album.HasIndex(a => new {a.normalisedArtist, a.normalisedTitle}).IsUnique();
        album.Property(a => a.title).IsRequired();

        album.HasMany(a => a.tracks)
            .WithOne(t => t.album)
            .HasForeignKey(t => t.albumId)
            .OnDelete(DeleteBehavior.Cascade);

        var track = modelBuilder.Entity<Track>();

        track.HasIndex(t => t.filePath).IsUnique();
        track.Property(t => t.filePath).IsRequired();
    }
}