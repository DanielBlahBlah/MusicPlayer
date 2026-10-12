using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using MusicPlayer.Settings;

namespace MusicPlayer;

public partial class MainWindow : Window
{
    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MusicPlayer",
        "settings.json"
        );

    public ObservableCollection<Album> Albums {get;} = new ObservableCollection<Album>();

    public MainWindow()
    {
        InitializeComponent();
        loadWindowSettings();

        DataContext = this;

        LoadMusic(@"C:\Users\mrtuu\Music");
        Closing += mainWindowClosing;
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

    private void LoadMusic(string musicFolder)
    {
        if (!Directory.Exists(musicFolder)) return;

        foreach (string folder in Directory.GetDirectories(musicFolder))
        {
            var tracks = Directory.GetFiles(folder).Where(file => new[] {".mp3", ".wav",".flac",".m4a",".wma",".aac"}.Contains(Path.GetExtension(file),StringComparer.OrdinalIgnoreCase)).ToList();
            if (tracks.Count == 0) continue;

            string cover = Directory.GetFiles(folder).FirstOrDefault(file => new[] {".jpg",".jpeg",".png"}.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)&& Path.GetFileNameWithoutExtension(file).Equals("cover", StringComparison.OrdinalIgnoreCase))?? "";
            Albums.Add(new Album
            {
                title = Path.GetFileName(folder),
                artist = "",
                coverPath = cover,
                genre = "",
                tracks = tracks
            });
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