using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicPlayer;

public sealed class ColumnAnchor : Panel
{
    public static readonly DependencyProperty SlotsProperty = DependencyProperty.Register(
        nameof(Slots), typeof(int), typeof(ColumnAnchor),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty GrowProperty = DependencyProperty.Register(
        nameof(GrowTo), typeof(double), typeof(ColumnAnchor),
        new FrameworkPropertyMetadata(0.0,
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public int Slots
    {
        get => (int)GetValue(SlotsProperty);
        set => SetValue(SlotsProperty, value);
    }

    public double GrowTo
    {
        get => (double)GetValue(GrowProperty);
        set => SetValue(GrowProperty, value);
    }

    private double scale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    private (double x, double width)[] place(int count, double totalPixels)
    {
        var slots = Math.Max(1, Slots);
        var slotWidth = Math.Floor(totalPixels / slots);
        var result = new(double x, double width)[count];

        for (var i = 0; i < count; i++)
        {
            if (i == 0)
            {
                var width = totalPixels - (slots - 1) * slotWidth;

                if (GrowTo > 0) width = Math.Min(totalPixels, Math.Max(width, Math.Ceiling(Math.Round(GrowTo * scale, 3))));

                result[i] = (0, width);
            }
            else
            {
                result[i] = (totalPixels - (slots - i) * slotWidth, slotWidth);
            }
        }
        return result;
    }

    protected override Size MeasureOverride(Size available)
    {
        var children = InternalChildren.Cast<UIElement>().ToList();
        var height = 0.0;

        if (double.IsInfinity(available.Width))
        {
            var total = 0.0;

            foreach (var child in children)
            {
                child.Measure(new Size(double.PositiveInfinity, available.Height));
                total += child.DesiredSize.Width;
                height = Math.Max(height, child.DesiredSize.Height);
            }

            return new Size(total, height);
        }

        var spots = place(children.Count, Math.Round(available.Width * scale));

        for (var i = 0; i < children.Count; i++)
        {
            children[i].Measure(new Size(spots[i].width /scale, available.Height));
            height = Math.Max(height, children[i].DesiredSize.Height);
        }

        return new Size(available.Width, height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var children = InternalChildren.Cast<UIElement>().ToList();
        var totalPixels = Math.Round(final.Width * scale);
        var spots = place(children.Count, totalPixels);

        for (var i =0; i < children.Count; i++) children[i].Arrange(new Rect(spots[i].x / scale, 0, spots[i].width/scale,final.Height));
        return final;
    }
}