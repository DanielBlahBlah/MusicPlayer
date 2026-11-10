using System.Windows;
using System.Windows.Controls;

namespace MusicPlayer;

public sealed class AlbumSpreadPanel : Panel
{
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(AlbumSpreadPanel),
        new FrameworkPropertyMetadata(1,FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
    
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(AlbumSpreadPanel),
        new FrameworkPropertyMetadata(160.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
    
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public static double SlotWidth(double width, int columns, double itemWidth)
    {
        columns = Math.Max(1, columns);
        var slot = width/ columns;

        if (columns > 1 && slot < itemWidth) slot = Math.Max(0, (width-itemWidth) / (columns - 1));

        return slot;
    }

    private double slotFor(double width) => Math.Max(ItemWidth, width/ Math.Max(1,Columns));

    protected override Size MeasureOverride(Size available)
    {
        var height = 0.0;

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(ItemWidth, available.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        var width = double.IsInfinity(available.Width) ? Math.Max(1, Columns) * ItemWidth : available.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var slot = slotFor(final.Width);
        var i = 0;

        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(Math.Floor(i * slot), 0, ItemWidth, final.Height));
            i++;
        }

        return final;
    }
}