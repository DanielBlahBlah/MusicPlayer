using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MusicPlayer;

public static class AsyncImage
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.RegisterAttached("Path", typeof(string), typeof(AsyncImage), new PropertyMetadata(null, onPathChanged));

    public static string? GetPath(DependencyObject d) => (string?)d.GetValue(PathProperty);
    public static void SetPath(DependencyObject d, string? value) => d.SetValue(PathProperty, value);

    private sealed class Request(Image image, string path)
    {
        public Image image {get;} = image;
        public string path {get;} = path;
        public bool cancelled;
    }

    private static readonly DependencyProperty requestProperty = DependencyProperty.RegisterAttached("Request", typeof(Request), typeof(AsyncImage));

    private static readonly List<Request> pending = new();
    private static bool pumping;

    public const double MaxLoadingSpeed = 1000;
    public static readonly Stopwatch clock = Stopwatch.StartNew();
    private static double lastOffset = double.NaN;
    private static double lastReading;
    private static double scrollSpeed;

    private static void onPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image) return;

        if (image.GetValue(requestProperty) is Request old) old.cancelled = true;

        image.Source = null;
        image.ClearValue(requestProperty);

        var path = e.NewValue as string;
        if (string.IsNullOrEmpty(path)) return;

        var request = new Request(image, path);
        image.SetValue(requestProperty, request);
        pending.Add(request);

        if (!pumping) _ = pumpAsync();
    }

    private static async Task pumpAsync()
    {
        pumping = true;

        try
        {
            while (true)
            {
                await Dispatcher.Yield(DispatcherPriority.Background);

                pending.RemoveAll(r => r.cancelled);
                if (pending.Count == 0) return;

                var scroller = pending.Select(r => findScroller(r.image)).FirstOrDefault(s => s != null);

                if (scroller != null)
                {
                    measureScrollSpeed(scroller);

                    if (scrollSpeed > MaxLoadingSpeed)
                    {
                        await Task.Delay(50);
                        continue;
                    }
                }

                var next = pickNext();
                pending.Remove(next);

                if (next.cancelled) continue;

                try
                {
                    var bitmap = await Task.Run(() => load(next.path));

                    if (!next.cancelled && bitmap != null) next.image.Source = bitmap;
                }
                catch (Exception)
                {
                    
                }
            }
        }
        finally
        {
            pumping = false;
        }
    }

    private static void measureScrollSpeed(ScrollViewer scroller)
    {
        var now = clock.Elapsed.TotalSeconds;
        var offset = scroller.VerticalOffset;

        if (double.IsNaN(lastOffset))
        {
            lastOffset = offset;
            lastReading = now;
            scrollSpeed = 0;
            return;
        }

        var elapsed = now - lastReading;
        if (elapsed < 0.05) return;

        scrollSpeed = Math.Abs(offset - lastOffset) / elapsed;
        lastOffset = offset;
        lastReading = now;
    }

    private static Request pickNext()
    {
        Request? best = null;
        var bestKey = (distance: double.MaxValue, top: double.MaxValue, left: double.MaxValue);

        foreach (var request in pending)
        {
            var key = locate(request.image);

            if (key.distance < 0)
            {
                request.cancelled = true;
                continue;
            }

            if (best == null || key.CompareTo(bestKey) < 0)
            {
                best = request;
                bestKey = key;
            }
        }

        return best ?? pending [0];
    }

    private static (double distance, double top, double left) locate(Image image)
    {
        if (!image.IsLoaded) return (-1,0,0);

        var scroller = findScroller(image);
        if (scroller == null) return (0,0,0);

        try
        {
            var bounds = image
                .TransformToAncestor(scroller)
                .TransformBounds(new Rect(0,0, image.ActualWidth, image.ActualHeight));

            var height = scroller.ActualHeight;

            var distance =
                bounds.Bottom < 0 ? -bounds.Bottom :
                bounds.Top > height ? bounds.Top - height :
                0;

            return (distance, bounds.Top, bounds.Left);
        }
        catch (InvalidOperationException)
        {
            return (-1,0,0);
        }
    }

    private static ScrollViewer? findScroller(DependencyObject start)
    {
        var current = VisualTreeHelper.GetParent(start);

        while (current != null)
        {
            if (current is ScrollViewer scroller) return scroller;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static BitmapImage? load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }
}