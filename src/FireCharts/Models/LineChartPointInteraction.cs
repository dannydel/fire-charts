namespace FireCharts.Models;

public sealed record LineChartPointInteraction<TItem>(
    TItem Item,
    int SeriesIndex,
    string SeriesName,
    int PointIndex,
    string Label,
    LineChartXValue X,
    double Y);
