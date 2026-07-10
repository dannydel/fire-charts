namespace FireCharts.Models;

public sealed record ClusteredBarChartSegment<TItem, TSegment>(
    TItem Item,
    TSegment Segment,
    int CategoryIndex,
    int SegmentIndex,
    string CategoryLabel,
    string SeriesLabel,
    double Value,
    string Fill,
    string HoverFill,
    SvgRect Rect,
    string AccessibleLabel,
    bool IsSelected,
    bool IsHovered,
    bool IsFocused)
{
    public string Key => $"{CategoryIndex}:{SegmentIndex}:{CategoryLabel}:{SeriesLabel}";
}
