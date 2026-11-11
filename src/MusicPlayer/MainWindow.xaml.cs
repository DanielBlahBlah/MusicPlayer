using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using MusicPlayer.LibraryDatabase;
using MusicPlayer.LibraryObjects;
using MusicPlayer.LibraryScanner;
using MusicPlayer.Player;
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

    private static string sortKey(string? text) => AlbumSort.sortKey(text);

    public ObservableCollection<GridRow> Rows { get;} = new ObservableCollection<GridRow>();

    public Queue queue { get;} = new();

    private AlbumDetail? openDetail;
    private List<Album> sortedAlbums = new();
    private int columns = 6;
    private double gridWidth = 1000;
    private int shownTrackColumns = 1;
    private const double tileSlotWidth = 170;

    private readonly SemaphoreSlim albumSync = new(1,1);
    private bool liveRefreshRunning;
    private bool swallowUp;

    private const double wheelSpeed = 3.0;

    public MainWindow()
    {
        InitializeComponent();
        loadWindowSettings();

        DataContext = this;

        Loaded += mainWindowLoaded;
        Closing += mainWindowClosing;
        KeyDown += mainWindowKeyDown;
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

                album.groupLetter = AlbumSort.Letter(album.title);
                album.sortTitle = AlbumSort.sortKey(album.title);
            }

            sortedAlbums = albums
                .OrderBy(a => a.groupLetter, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(a => a.sortTitle, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(a => sortKey(a.albumArtist), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(a => a.id)
                .ToList();

            refreshRows();
        }
        finally
        {
            albumSync.Release();
        }
    }

    private int trackColumnsNow() => openDetail?.ChooseColumns(gridWidth - 356) ?? 1;
    private void refreshRows()
    {
        shownTrackColumns = trackColumnsNow();
        syncRows(buildRows(sortedAlbums, columns, openDetail, shownTrackColumns));
    }

    private static List<GridRow> buildRows(List<Album> albums, int columns, AlbumDetail? detail, int trackColumns)
    {
        var rows = new List<GridRow>();
        var current = new List<Album>();
        string? letter = null;
        int? openColumn = null;

        void finishRow()
        {
            if (current.Count == 0) return;
            rows.Add(new AlbumRow(current, columns));

            if (detail != null && openColumn != null)
            {
                rows.Add(new DetailRow(detail, openColumn.Value,columns, trackColumns));
                openColumn = null;
            }

            current = new List<Album>();
        }

        foreach (var album in albums)
        {
            if (album.groupLetter != letter)
            {
                finishRow();
                letter = album.groupLetter;
                rows.Add(new HeaderRow(letter ?? "#"));
            }

            if (detail != null && album.id == detail.album.id) openColumn = current.Count;

            current.Add(album);
            if (current.Count == columns) finishRow();
        }

        finishRow();
        return rows;
    }

    private void syncRows(List<GridRow> target)
    {
        for (var i = 0; i < target.Count; i++)
        {
            if (i >= Rows.Count) Rows.Add(target[i]);
            else if (Rows[i] is DetailRow current && target[i] is DetailRow next && ReferenceEquals(current.detail, next.detail)) current.UpdateFrom(next);
            else if (!sameRow(Rows[i], target[i])) Rows[i] = target[i];
        }

        while (Rows.Count > target.Count) Rows.RemoveAt(Rows.Count - 1);
    }

    private static bool sameRow(GridRow a, GridRow b) => (a,b) switch
    {
        (HeaderRow x, HeaderRow y) => x.letter == y.letter,
        (AlbumRow x, AlbumRow y) => x.albums.Count == y.albums.Count && x.albums.Zip(y.albums).All(pair => pair.First.id == pair.Second.id && !albumChanged(pair.First, pair.Second)),
        _ => false
    };

    private void gridAreaSizeChanged(object sender, SizeChangedEventArgs e)
    {
        gridWidth = e.NewSize.Width;
        var available = e.NewSize.Width - 20 - SystemParameters.VerticalScrollBarWidth;
        var newColumns = Math.Max(1,(int)(available / tileSlotWidth));

        if (newColumns == columns && trackColumnsNow() == shownTrackColumns) return;

        columns = newColumns;
        refreshRows();
    }

    private static bool albumChanged(Album a, Album b) =>
        a.title != b.title ||
        a.albumArtist != b.albumArtist ||
        a.artworkPath != b.artworkPath ||
        a.artworkHash != b.artworkHash ||
        a.thumbnailPath != b.thumbnailPath ||
        a.releaseYear != b.releaseYear ||
        a.genre != b.genre ||
        a.trackCount != b.trackCount;

    private static string? existingFile(string? path) => path != null && File.Exists(path) ? path : null;

    private async void albumClickedDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            swallowUp = false;
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is not Album album) return;

        swallowUp = true;

        try
        {
            await queueAlbumAsync(album);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private async void albumClicked(object sender, MouseButtonEventArgs e)
    {
        if (swallowUp)
        {
            swallowUp = false;
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is not Album album) return;

        try
        {
            if (openDetail?.album.id == album.id) closeAlbum();
            else await openAlbumAsync(album);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private async Task<List<Track>> loadTracksAsync(int albumId)
    {
        await using var db = MusicDatabase.CreateContext(settingsDirectory);

        return await db.tracks
            .AsNoTracking()
            .Where(t => t.albumId == albumId)
            .OrderBy(t => t.discNumber ?? 1)
            .ThenBy(t => t.trackNumber ?? int.MaxValue)
            .ThenBy(t => t.title)
            .ToListAsync();
    }

    private async Task openAlbumAsync(Album album)
    {
        var tracks = await loadTracksAsync(album.id);

        openDetail = AlbumDetail.Build(album, tracks);
        refreshRows();

        await Dispatcher.Yield(DispatcherPriority.Background);

        var row = Rows.OfType<DetailRow>().FirstOrDefault();

        if (row != null && albumGrid.ItemContainerGenerator.ContainerFromItem(row) is FrameworkElement panel) panel.BringIntoView();
    }

    private async Task queueAlbumAsync(Album album)
    {
        var tracks = await loadTracksAsync(album.id);

        queue.Enqueue(tracks);
    }

    private void closeAlbum()
    {
        openDetail = null;
        refreshRows();
    }

    private void albumDetailClose(object sender, MouseButtonEventArgs e) => closeAlbum();

    private void mainWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && openDetail != null)
        {
            closeAlbum();
            e.Handled = true;
        }
    }

    private void gridPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (findScrollViewer((DependencyObject)sender) is not {} scroller) return;

        var notches = e.Delta / 120.0;
        var pixels = notches * SystemParameters.WheelScrollLines * 16 * wheelSpeed;

        scroller.ScrollToVerticalOffset(scroller.VerticalOffset - pixels);
        e.Handled = true;
    }

    private static ScrollViewer? findScrollViewer(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is ScrollViewer scroller) return scroller;

            var found = findScrollViewer(child);
            if (found != null) return found;
        }

        return null;
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

    private void mainWindowClosing(object? sender, CancelEventArgs e)
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