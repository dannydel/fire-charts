namespace FireCharts.Models;

public sealed record ClusteredBarChartContext<TItem, TSegment>(
    double Width,
    double Height,
    double ChartAreaLeft,
    double ChartAreaTop,
    double ChartAreaRight,
    double ChartAreaBottom,
    double ChartAreaWidth,
    double ChartAreaHeight,
    bool Horizontal,
    double MaxValue,
    IReadOnlyList<ClusteredBarChartSegment<TItem, TSegment>> Segments);
