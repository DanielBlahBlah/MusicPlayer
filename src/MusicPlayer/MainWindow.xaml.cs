using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using MusicPlayer.LibraryDatabase;
using MusicPlayer.LibraryObjects;
using MusicPlayer.LibraryScanner;
using MusicPlayer.Settings;

namespace MusicPlayer;

public partial class MainWindow : Window
{
    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MusicPlayer",
        "settings.json"
        );

    private string settingsDirectory => Path.GetDirectoryName(settingsPath)!;

    private static readonly string musicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

    private CancellationTokenSource? scanCts;

    private static readonly string[] ignoredWords = ["the ", "a ", "an "];

    public ObservableCollection<Album> Albums {get;} = new ObservableCollection<Album>();

    private readonly SemaphoreSlim albumSync = new(1,1);
    private bool liveRefreshRunning;

    public MainWindow()
    {
        InitializeComponent();
        loadWindowSettings();

        DataContext = this;

        Loaded += mainWindowLoaded;
        Closing += mainWindowClosing;
    }

    private async void mainWindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await loadAlbumsAsync(ensureCreated: true);

            await scanLibraryAsync();

            await loadAlbumsAsync();
        }
        catch (OperationCanceledException)
        {

        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
        }
    }

    private async Task scanLibraryAsync()
    {
        var scanner = new MusicScanner(settingsDirectory);
        var progress = new Progress<int>(count => Title = $"Scanning... {count}");

        var albumsChanged = new Progress<IReadOnlyCollection<int>>(async ids =>
        {
            if (liveRefreshRunning) return;
            liveRefreshRunning = true;

            try { await loadAlbumsAsync();}
            catch (Exception) {}
            finally {liveRefreshRunning = false;}
        });

        scanCts = new CancellationTokenSource();

        var result = await Task.Run(() => scanner.ScanAsync(musicFolder, progress, albumsChanged, scanCts.Token));

        Title = "Music Player";

        MessageBox.Show($"Found {result.filesFound}, added {result.filesAdded}, updated {result.filesUpdated}, "+
            $"removed {result.filesRemoved}, skipped {result.filesSkipped}\n\n" +
            string.Join("\n", result.Errors.Take(10)));
    }

    private async Task loadAlbumsAsync(bool ensureCreated = false)
    {
        await albumSync.WaitAsync();

        try
        {
            List<Album> albums;

            await using (var db = MusicDatabase.CreateContext(settingsDirectory))
            {
                if (ensureCreated) await db.Database.EnsureCreatedAsync();

                albums = await db.albums.AsNoTracking().ToListAsync();
            }

            foreach (var album in albums)
            {
                album.thumbnailPath = existingFile(CoverPaths.Small(settingsDirectory, album.artworkHash));
                album.mediumPath = existingFile(CoverPaths.Medium(settingsDirectory, album.artworkHash));
            }

            var sorted = albums
                .OrderBy(a => sortKey(a.title), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(a => a.id)
                .ToList();

            syncAlbums(sorted);
        }
        finally
        {
            albumSync.Release();
        }
    }

    private void syncAlbums(List<Album> target)
    {
        for (var i = 0; i < target.Count; i++)
        {
            var album = target[i];

            if (i < Albums.Count && Albums[i].id == album.id)
            {
                if (albumChanged(Albums[i], album)) Albums[i] = album;
                continue;
            }

            var existing = -1;
            for (var j = i + 1; j < Albums.Count; j++)
            {
                if (Albums[j].id == album.id) {existing = j; break;}
            }

            if (existing >= 0)
            {
                Albums.Move(existing,i);
                if (albumChanged(Albums[i], album)) Albums[i] = album;
            }
            else
            {
                Albums.Insert(i, album);
            }
        }

        while (Albums.Count > target.Count) Albums.RemoveAt(Albums.Count - 1);
    }

    private static bool albumChanged(Album a, Album b) =>
        a.title != b.title ||
        a.albumArtist != b.albumArtist ||
        a.artworkPath != b.artworkPath ||
        a.artworkHash != b.artworkHash ||
        a.thumbnailPath != b.thumbnailPath ||
        a.releaseYear != b.releaseYear ||
        a.trackCount != b.trackCount;
    
    private static string? existingFile(string? path) => path != null && File.Exists(path) ? path : null;

    private static string sortKey(string? text)
    {
        var value = (text ?? "").Trim();

        foreach (var word in ignoredWords)
        {
            if (value.Length > word.Length && value.StartsWith(word, StringComparison.CurrentCultureIgnoreCase))
            {
                return value[word.Length..].TrimStart();
            }
        }

        return value;
    }

    private void loadWindowSettings()
    {
        if (!File.Exists(settingsPath))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<ProjectSettings>(json);

            if (settings == null) return;

            Width = settings.Width;
            Height = settings.Height;

            if (settings.IsMaximised)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = settings.Left;
                Top = settings.Top;
            }

            sidebar.Width = new GridLength(settings.sidebarWidth);
        }
        catch
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            sidebar.Width = new GridLength(100);
        }
    }

    private void mainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        scanCts?.Cancel();

        var settings = new ProjectSettings
        {
            Width = RestoreBounds.Width,
            Height = RestoreBounds.Height,
            Left = RestoreBounds.Left,
            Top = RestoreBounds.Top,
            IsMaximised = WindowState == WindowState.Maximized,
            sidebarWidth = sidebar.ActualWidth
        };

        var directory = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions {WriteIndented = true});

        File.WriteAllText(settingsPath,json);
    }
}