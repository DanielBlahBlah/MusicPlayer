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

    public MainWindow()
    {
        InitializeComponent();
        loadWindowSettings();

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