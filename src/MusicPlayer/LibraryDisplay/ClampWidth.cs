using System.Windows;
using System.Windows.Controls;

namespace MusicPlayer;

public sealed class ClampWidth : Decorator
{
    private double limit = double.PositiveInfinity;

    protected override Size MeasureOverride(Size available)
    {
        limit = available.Width;

        if (Child == null) return new Size();

        Child.Measure(available);

        var width = double.IsInfinity(available.Width)
            ? Child.DesiredSize.Width
            : Math.Min(Child.DesiredSize.Width, available.Width);

        return new Size(width, Child.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var width = double.IsInfinity(limit) ? final.Width : Math.Min(final.Width, limit);
        Child?.Arrange(new Rect(0,0, width, final.Height));
        return final;
    }
}