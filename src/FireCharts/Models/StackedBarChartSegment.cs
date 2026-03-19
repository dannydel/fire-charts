namespace FireCharts.Models;

public sealed record StackedBarChartSegment<TItem, TSegment>(
    TItem Item,
    TSegment Segment,
    int BarIndex,
    int SegmentIndex,
    string BarLabel,
    string SegmentLabel,
    double Value,
    double TotalValue,
    string Fill,
    string HoverFill,
    SvgRect Rect,
    string AccessibleLabel,
    bool IsSelected,
    bool IsHovered,
    bool IsFocused)
{
    public string Key => $"{BarIndex}:{SegmentIndex}:{BarLabel}:{SegmentLabel}";
}
