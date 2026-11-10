using System.Windows;
using System.Windows.Controls;

namespace MusicPlayer;

public sealed class ClampPointer : Panel
{
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(ClampPointer),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ColumnProperty = DependencyProperty.Register(
        nameof(Column), typeof(int), typeof(ClampPointer),
        new FrameworkPropertyMetadata(0,FrameworkPropertyMetadataOptions.AffectsArrange));
    
    public static readonly DependencyProperty TileWidthProperty = DependencyProperty.Register(
        nameof(TileWidth), typeof(double), typeof(ClampPointer),
        new FrameworkPropertyMetadata(160.0, FrameworkPropertyMetadataOptions.AffectsArrange));
    
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public int Column
    {
        get => (int)GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
    }

    public double TileWidth
    {
        get => (double)GetValue(TileWidthProperty);
        set => SetValue(TileWidthProperty, value);
    }

    protected override Size MeasureOverride(Size available)
    {
        var height = 0.0;

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(double.IsInfinity(available.Width) ? 0 : available.Width, height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var slot = AlbumSpreadPanel.SlotWidth(final.Width, Columns, TileWidth);

        foreach (UIElement child in InternalChildren)
        {
            var x = Math.Floor(Column * slot) + TileWidth /2 - child.DesiredSize.Width /2;
            child.Arrange(new Rect(x,0, child.DesiredSize.Width, final.Height));
        }

        return final;
    }
}