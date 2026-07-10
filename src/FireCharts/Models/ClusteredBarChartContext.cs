namespace FireCharts.Models;

public sealed record ClusteredBarChartContext<TItem, TSegment>(
    PlotArea Plot,
    bool Horizontal,
    double MaxValue,
    IReadOnlyList<ClusteredBarChartSegment<TItem, TSegment>> Segments)
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
