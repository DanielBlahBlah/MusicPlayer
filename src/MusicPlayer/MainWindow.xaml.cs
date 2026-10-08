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
        "window.json"
        );

    public MainWindow()
    {
        InitializeComponent();
        LoadWindowSettings();

        Closing += MainWindow_Closing;
    }

    private void LoadWindowSettings()
    {
        if (!File.Exists(settingsPath))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<WindowSettings>(json);

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
        }
        catch
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var settings = new WindowSettings
        {
            Width = RestoreBounds.Width,
            Height = RestoreBounds.Height,
            Left = RestoreBounds.Left,
            Top = RestoreBounds.Top,
            IsMaximised = WindowState == WindowState.Maximized
        };

        var directory = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions {WriteIndented = true});

        File.WriteAllText(settingsPath,json);
    }
}