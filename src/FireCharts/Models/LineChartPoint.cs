namespace FireCharts.Models;

public sealed record LineChartPoint<TItem>(
    TItem Item,
    int SeriesIndex,
    string SeriesName,
    int PointIndex,
    string Label,
    LineChartXValue X,
    double Y,
    SvgPoint Coordinates,
    string Stroke,
    string HoverStroke,
    string Fill,
    bool IsHighlighted,
    bool IsSelected,
    bool IsHovered,
    bool IsFocused,
    string AccessibleLabel)
{
    public string Key => $"{SeriesIndex}:{PointIndex}:{Label}";
}
