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
            await loadAlbumsAsync();

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
            try { await showAlbumsAsync(ids);}
            catch (Exception) {}
        });

        scanCts = new CancellationTokenSource();

        var result = await Task.Run(() => scanner.ScanAsync(musicFolder, progress, albumsChanged, scanCts.Token));

        Title = "Music Player";

        MessageBox.Show($"Found {result.filesFound}, added {result.filesAdded}, updated {result.filesUpdated}, "+
            $"removed {result.filesRemoved}, skipped {result.filesSkipped}\n\n" +
            string.Join("\n", result.Errors.Take(10)));
    }

    private async Task showAlbumsAsync(IReadOnlyCollection<int> ids)
    {
        await using var db = MusicDatabase.CreateContext(settingsDirectory);

        var loaded = await db.albums
            .AsNoTracking()
            .Where(a => ids.Contains(a.id))
            .ToListAsync();

        foreach (var album in loaded)
        {
            album.thumbnailPath = CoverPaths.Small(settingsDirectory, album.artworkHash);
            album.mediumPath = CoverPaths.Medium(settingsDirectory, album.artworkHash);

            var index = -1;
            for (var i = 0; i < Albums.Count; i++)
            {
                if (Albums[i].id == album.id) {index = 1; break;}
            }

            if (index >= 0) Albums[index] = album;
            else Albums.Insert(sortedIndex(album), album);
        }
    }

    private int sortedIndex(Album album)
    {
        var key = sortKey(album.title);
        var low = 0;
        var high = Albums.Count;

        while (low < high)
        {
            var mid = (low + high) / 2;

            if (StringComparer.CurrentCultureIgnoreCase.Compare(sortKey(Albums[mid].title), key) <= 0) low = mid + 1;
            else high = mid;
        }

        return low;
    }

    private async Task loadAlbumsAsync()
    {
        await using var db = MusicDatabase.CreateContext(settingsDirectory);
        await db.Database.EnsureCreatedAsync();

        var albums = await db.albums
            .AsNoTracking()
            .ToListAsync();

        foreach (var album in albums)
        {
            album.thumbnailPath = CoverPaths.Small(settingsDirectory, album.artworkHash);
            album.mediumPath = CoverPaths.Medium(settingsDirectory, album.artworkHash);
        }

        var sorted = albums
            .OrderBy(a => sortKey(a.title), StringComparer.CurrentCultureIgnoreCase);

        Albums.Clear();
        foreach (var album in sorted) Albums.Add(album);
    }

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