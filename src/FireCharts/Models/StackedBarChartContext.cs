namespace FireCharts.Models;

public sealed record StackedBarChartContext<TItem, TSegment>(
    PlotArea Plot,
    bool Horizontal,
    double MaxValue,
    IReadOnlyList<StackedBarChartSegment<TItem, TSegment>> Segments)
{
    public double Width => Plot.SurfaceWidth;
    public double Height => Plot.SurfaceHeight;
    public double ChartAreaLeft => Plot.Left;
    public double ChartAreaTop => Plot.Top;
    public double ChartAreaRight => Plot.Right;
    public double ChartAreaBottom => Plot.Bottom;
    public double ChartAreaWidth => Plot.Width;
    public double ChartAreaHeight => Plot.Height;
}
