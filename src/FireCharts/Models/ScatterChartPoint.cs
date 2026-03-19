namespace FireCharts.Models;

public sealed record ScatterChartPoint<TItem>(
    TItem Item,
    int SeriesIndex,
    string SeriesName,
    int PointIndex,
    string Label,
    LineChartXValue X,
    double Y,
    SvgPoint Coordinates,
    string Fill,
    string HoverFill,
    double Radius,
    bool IsSelected,
    bool IsHovered,
    bool IsFocused,
    string AccessibleLabel)
{
    public string Key => $"{SeriesIndex}:{PointIndex}:{Label}";
}
